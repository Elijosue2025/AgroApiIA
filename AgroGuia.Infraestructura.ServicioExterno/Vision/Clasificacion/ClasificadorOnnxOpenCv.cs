using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;

/// <summary>
/// Clasifica con ONNX Runtime. OpenCV solo lee y redimensiona la foto.
/// CvDnn.Forward de este MobileNet tumba el proceso (código -1) y un try/catch no lo alcanza.
/// Entrada: RGB 0..1, 224x224. La normalización de ImageNet ya va dentro del ONNX.
/// </summary>
public sealed class ClasificadorOnnxOpenCv : IClasificadorImagen, IDisposable
{
    private readonly VisionOptions _opt;
    private readonly ILogger<ClasificadorOnnxOpenCv> _log;
    private readonly object _sync = new();
    private readonly Lazy<Cargado> _cargado;

    private sealed record Cargado(
        InferenceSession? Sesion,
        string? NombreEntrada,
        IReadOnlyList<EtiquetaVision> Etiquetas,
        string? Modelo,
        string? Detalle);

    public ClasificadorOnnxOpenCv(IOptions<VisionOptions> opt, ILogger<ClasificadorOnnxOpenCv> log)
    {
        _opt = opt.Value;
        _log = log;
        _cargado = new Lazy<Cargado>(Cargar, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool Disponible => _cargado.Value.Sesion is not null;

    public EstadoVision Estado
    {
        get
        {
            var c = _cargado.Value;
            return new EstadoVision(c.Sesion is not null, c.Modelo, c.Etiquetas.Count, c.Detalle);
        }
    }

    [Obsolete]
    public IReadOnlyList<DeteccionVision> Clasificar(Mat imagenBgr, int topK)
    {
        var c = _cargado.Value;
        if (c.Sesion is null || c.NombreEntrada is null)
            return Array.Empty<DeteccionVision>();

        if (imagenBgr.Empty())
            throw new InvalidOperationException("La imagen está vacía.");

        var tensor = ATensorRgb01(imagenBgr);
        float[] salida;
        lock (_sync)
        {
            var entrada = NamedOnnxValue.CreateFromTensor(c.NombreEntrada, tensor);
            using var resultados = c.Sesion.Run(new[] { entrada });
            salida = resultados.First().AsEnumerable<float>().ToArray();
        }

        if (salida.Length != c.Etiquetas.Count)
            throw new InvalidOperationException(
                $"El modelo devolvió {salida.Length} clases pero etiquetas.json define {c.Etiquetas.Count}.");

        var prob = AProbabilidades(salida);
        return Enumerable.Range(0, prob.Length)
            .OrderByDescending(i => prob[i])
            .Take(Math.Max(1, topK))
            .Select(i => new DeteccionVision(i, c.Etiquetas[i], prob[i]))
            .ToList();
    }

    [Obsolete]
    private DenseTensor<float> ATensorRgb01(Mat imagenBgr)
    {
        using var rgb = new Mat();
        Cv2.CvtColor(imagenBgr, rgb, ColorConversionCodes.BGR2RGB);
        using var chica = new Mat();
        Cv2.Resize(rgb, chica, new Size(_opt.AnchoEntrada, _opt.AltoEntrada), 0, 0, InterpolationFlags.Area);

        int h = chica.Rows;
        int w = chica.Cols;
        var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
        var idx = chica.GetGenericIndexer<Vec3b>();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vec3b p = idx[y, x];
                tensor[0, 0, y, x] = p.Item0 / 255f;
                tensor[0, 1, y, x] = p.Item1 / 255f;
                tensor[0, 2, y, x] = p.Item2 / 255f;
            }
        }
        return tensor;
    }

    private Cargado Cargar()
    {
        var rutaModelo = ResolverRuta(_opt.RutaModeloOnnx);
        var rutaEtiquetas = ResolverRuta(_opt.RutaEtiquetas);
        var nombreModelo = Path.GetFileName(rutaModelo);

        if (!File.Exists(rutaModelo))
        {
            var detalle = $"No se encontró '{nombreModelo}'.";
            _log.LogWarning("[Vision] {Detalle} Ruta: {Ruta}", detalle, rutaModelo);
            return new Cargado(null, null, Array.Empty<EtiquetaVision>(), nombreModelo, detalle);
        }

        if (!File.Exists(rutaEtiquetas))
        {
            var detalle = $"No se encontró '{Path.GetFileName(rutaEtiquetas)}'.";
            _log.LogWarning("[Vision] {Detalle} Ruta: {Ruta}", detalle, rutaEtiquetas);
            return new Cargado(null, null, Array.Empty<EtiquetaVision>(), nombreModelo, detalle);
        }

        try
        {
            var etiquetas = CatalogoEtiquetas.Cargar(rutaEtiquetas);
            var opciones = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC,
                IntraOpNumThreads = 2,
                InterOpNumThreads = 1
            };
            var sesion = new InferenceSession(rutaModelo, opciones);
            var entrada = sesion.InputMetadata.Keys.FirstOrDefault()
                ?? throw new InvalidDataException("El ONNX no declara entrada.");

            _log.LogInformation(
                "[Vision] ONNX Runtime cargó '{Modelo}' ({N} clases, entrada '{Entrada}').",
                nombreModelo, etiquetas.Count, entrada);
            return new Cargado(sesion, entrada, etiquetas, nombreModelo, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Vision] No se pudo cargar '{Modelo}' con ONNX Runtime.", nombreModelo);
            return new Cargado(null, null, Array.Empty<EtiquetaVision>(), nombreModelo,
                "No se pudo cargar el modelo. La API sigue activa. " + ex.Message);
        }
    }

    private static string ResolverRuta(string ruta) =>
        Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);

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
            _cargado.Value.Sesion?.Dispose();
    }
}