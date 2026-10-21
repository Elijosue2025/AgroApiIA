using System.Runtime.InteropServices;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;

/// <summary>
/// Clasificador de imágenes con un modelo ONNX ejecutado por el módulo DNN de OpenCV (CPU).
///
///  - Singleton: la red se carga UNA vez, de forma perezosa y sin lanzar excepciones. Si falta el modelo
///    o las etiquetas, la API arranca igual y <see cref="Disponible"/> queda en false.
///  - cv::dnn::Net no admite inferencias concurrentes: se serializa con un lock (una inferencia de
///    clasificación en CPU toma decenas o pocos cientos de ms).
///  - Salida esperada: vector de N clases (logits o probabilidades). Si son logits se aplica softmax.
/// </summary>
public sealed class ClasificadorOnnxOpenCv : IClasificadorImagen, IDisposable
{
    private readonly VisionOptions _opt;
    private readonly ILogger<ClasificadorOnnxOpenCv> _log;
    private readonly object _sync = new();
    private readonly Lazy<Cargado> _cargado;

    private sealed record Cargado(
        Net? Red,
        IReadOnlyList<EtiquetaVision> Etiquetas,
        string? Modelo,
        string? Detalle);

    public ClasificadorOnnxOpenCv(IOptions<VisionOptions> opt, ILogger<ClasificadorOnnxOpenCv> log)
    {
        _opt = opt.Value;
        _log = log;
        _cargado = new Lazy<Cargado>(Cargar, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool Disponible => _cargado.Value.Red is not null;

    public EstadoVision Estado
    {
        get
        {
            var c = _cargado.Value;
            return new EstadoVision(c.Red is not null, c.Modelo, c.Etiquetas.Count, c.Detalle);
        }
    }

    public IReadOnlyList<DeteccionVision> Clasificar(Mat imagenBgr, int topK)
    {
        var c = _cargado.Value;
        if (c.Red is null) return Array.Empty<DeteccionVision>();

        // OpenCV aplica la media en el orden de los canales de la imagen (BGR), salvo que swapRB sea true:
        // entonces espera la media ya en orden RGB. Se arma según corresponda.
        var media = _opt.IntercambiarRB
            ? new Scalar(_opt.MediaR, _opt.MediaG, _opt.MediaB)
            : new Scalar(_opt.MediaB, _opt.MediaG, _opt.MediaR);

        float[] salida;
        lock (_sync)
        {
            // Redimensiona (sin recortar) a AnchoEntrada x AltoEntrada, resta la media, escala y reordena BGR -> RGB.
            using var blob = CvDnn.BlobFromImage(
                imagenBgr,
                _opt.Escala,
                new Size(_opt.AnchoEntrada, _opt.AltoEntrada),
                media,
                _opt.IntercambiarRB,
                false);

            c.Red.SetInput(blob);
            using var resultado = c.Red.Forward();
            salida = CopiarAFloat(resultado);
        }

        if (salida.Length != c.Etiquetas.Count)
            throw new InvalidOperationException(
                $"El modelo devolvió {salida.Length} clases pero etiquetas.json define {c.Etiquetas.Count}. " +
                "Revisa que el orden y la cantidad de etiquetas coincidan con el entrenamiento.");

        var prob = AProbabilidades(salida);

        return Enumerable.Range(0, prob.Length)
            .OrderByDescending(i => prob[i])
            .Take(Math.Max(1, topK))
            .Select(i => new DeteccionVision(i, c.Etiquetas[i], prob[i]))
            .ToList();
    }

    // ---------------------------------------------------------------- carga

    private Cargado Cargar()
    {
        var rutaModelo = ResolverRuta(_opt.RutaModeloOnnx);
        var rutaEtiquetas = ResolverRuta(_opt.RutaEtiquetas);
        var nombreModelo = Path.GetFileName(rutaModelo);

        if (!File.Exists(rutaModelo))
        {
            var detalle = $"No se encontró el modelo '{nombreModelo}'. Colócalo en la carpeta ModelosVision (ver su README).";
            _log.LogWarning("[Vision] {Detalle} Ruta buscada: {Ruta}", detalle, rutaModelo);
            return new Cargado(null, Array.Empty<EtiquetaVision>(), nombreModelo, detalle);
        }

        if (!File.Exists(rutaEtiquetas))
        {
            var detalle = $"No se encontró el archivo de etiquetas '{Path.GetFileName(rutaEtiquetas)}'.";
            _log.LogWarning("[Vision] {Detalle} Ruta buscada: {Ruta}", detalle, rutaEtiquetas);
            return new Cargado(null, Array.Empty<EtiquetaVision>(), nombreModelo, detalle);
        }

        try
        {
            var etiquetas = CatalogoEtiquetas.Cargar(rutaEtiquetas);

            var red = CvDnn.ReadNetFromOnnx(rutaModelo);
            if (red.Empty())
            {
                red.Dispose();
                throw new InvalidDataException("OpenCV no pudo leer el modelo ONNX (red vacía).");
            }

            red.SetPreferableBackend(OpenCvSharp.Dnn.Backend.OPENCV);
            red.SetPreferableTarget(OpenCvSharp.Dnn.Target.CPU);

            _log.LogInformation("[Vision] Modelo '{Modelo}' cargado con {N} clases.", nombreModelo, etiquetas.Count);
            return new Cargado(red, etiquetas, nombreModelo, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Vision] No se pudo cargar el modelo '{Modelo}'.", nombreModelo);
            return new Cargado(null, Array.Empty<EtiquetaVision>(), nombreModelo,
                "El modelo o las etiquetas no se pudieron cargar. Revisa el log del servidor.");
        }
    }

    private static string ResolverRuta(string ruta) =>
        Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);

    // ---------------------------------------------------------------- salida

    private static float[] CopiarAFloat(Mat m)
    {
        // La salida de DNN es float32 de un canal (4 bytes por elemento). Si no, se leería basura.
        if (m.ElemSize() != sizeof(float))
            throw new InvalidOperationException(
                "La salida del modelo no es float32 de un canal. Exporta el ONNX en FP32.");

        var n = checked((int)m.Total());
        var datos = new float[n];

        // Marshal.Copy requiere memoria contigua. La salida de DNN lo es, pero se asegura.
        using var contigua = m.IsContinuous() ? null : m.Clone();
        var origen = contigua ?? m;

        Marshal.Copy(origen.Data, datos, 0, n);
        return datos;
    }

    /// <summary>Si el vector ya es una distribución de probabilidad se deja igual; si son logits, softmax estable.</summary>
    private static float[] AProbabilidades(float[] v)
    {
        var enRango = true;
        double suma = 0;
        foreach (var x in v)
        {
            if (x < 0f || x > 1f) enRango = false;
            suma += x;
        }
        if (enRango && Math.Abs(suma - 1.0) < 0.01) return v;

        var max = v.Max();
        var exp = new double[v.Length];
        double total = 0;
        for (var i = 0; i < v.Length; i++)
        {
            exp[i] = Math.Exp(v[i] - max);
            total += exp[i];
        }

        var p = new float[v.Length];
        for (var i = 0; i < v.Length; i++) p[i] = (float)(exp[i] / total);
        return p;
    }

    public void Dispose()
    {
        if (_cargado.IsValueCreated)
            _cargado.Value.Red?.Dispose();
    }
}
