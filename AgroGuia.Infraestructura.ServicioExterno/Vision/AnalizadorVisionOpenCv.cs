using System.Diagnostics;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Lesiones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision;

/// <summary>
/// Orquesta el análisis de una foto: valida -> decodifica -> limita tamaño -> daño por color (siempre)
/// -> clasificación con el modelo ONNX (si está disponible). Es el único lugar que decodifica la imagen.
/// </summary>
public sealed class AnalizadorVisionOpenCv : IAnalizadorVision
{
    private readonly IClasificadorImagen _clasificador;
    private readonly IAnalizadorLesiones _lesiones;
    private readonly VisionOptions _opt;
    private readonly ILogger<AnalizadorVisionOpenCv> _log;

    public AnalizadorVisionOpenCv(
        IClasificadorImagen clasificador,
        IAnalizadorLesiones lesiones,
        IOptions<VisionOptions> opt,
        ILogger<AnalizadorVisionOpenCv> log)
    {
        _clasificador = clasificador;
        _lesiones = lesiones;
        _opt = opt.Value;
        _log = log;
    }

    public EstadoVision ObtenerEstado() => _clasificador.Estado;

    // El trabajo es CPU-bound: se saca del hilo de la petición.
    public Task<ResultadoVision> AnalizarAsync(byte[] imagen, CancellationToken ct = default) =>
        Task.Run(() => Analizar(imagen, ct), ct);

    private ResultadoVision Analizar(byte[] bytes, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();

        var error = ImagenValidador.Validar(bytes, _opt.MaxBytesImagen);
        if (error is not null) return ResultadoVision.Fallo(error);

        try
        {
            using var original = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (original.Empty())
                return ResultadoVision.Fallo("No se pudo leer la imagen (archivo dañado o formato no soportado).");

            ct.ThrowIfCancellationRequested();

            using var reducida = ReducirSiEsNecesario(original);
            var imagen = reducida ?? original;

            var lesiones = _lesiones.Analizar(imagen);

            ct.ThrowIfCancellationRequested();

            IReadOnlyList<DeteccionVision> detecciones = Array.Empty<DeteccionVision>();
            if (_clasificador.Disponible)
            {
                detecciones = _clasificador.Clasificar(imagen, _opt.TopK)
                    .Where(d => d.Confianza >= _opt.ConfianzaMinima)
                    .ToList();
            }

            _log.LogInformation(
                "[Vision] {W}x{H} px | modelo {Modelo} | {N} detecciones | daño {Pct}% | {Ms} ms",
                imagen.Width, imagen.Height, _clasificador.Disponible ? "sí" : "no",
                detecciones.Count, lesiones.PorcentajeAfectado, reloj.ElapsedMilliseconds);

            return new ResultadoVision(
                Exito: true,
                ErrorMensaje: string.Empty,
                Detecciones: detecciones,
                Lesiones: lesiones,
                ModeloDisponible: _clasificador.Disponible,
                Ancho: original.Width,
                Alto: original.Height,
                TiempoMs: reloj.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Vision] Error analizando la imagen");
            return ResultadoVision.Fallo("Ocurrió un error al procesar la imagen. Intenta con otra foto.");
        }
    }

    /// <summary>Devuelve una copia reducida si el lado mayor supera MaxLadoPx; si no, null (se usa la original).</summary>
    private Mat? ReducirSiEsNecesario(Mat original)
    {
        var lado = Math.Max(original.Width, original.Height);
        if (_opt.MaxLadoPx <= 0 || lado <= _opt.MaxLadoPx) return null;

        var factor = (double)_opt.MaxLadoPx / lado;
        var nueva = new Size(
            Math.Max(1, (int)Math.Round(original.Width * factor)),
            Math.Max(1, (int)Math.Round(original.Height * factor)));

        var salida = new Mat();
        Cv2.Resize(original, salida, nueva, 0, 0, InterpolationFlags.Area);
        return salida;
    }
}
