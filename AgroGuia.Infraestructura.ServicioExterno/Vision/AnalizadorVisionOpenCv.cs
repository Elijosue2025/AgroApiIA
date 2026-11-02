using System.Diagnostics;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Lesiones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision;

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

    public Task<ResultadoVision> AnalizarAsync(byte[] imagen, CancellationToken ct = default) =>
        Task.Run(() => Analizar(imagen), ct);

    private ResultadoVision Analizar(byte[] bytes)
    {
        var reloj = Stopwatch.StartNew();
        var error = ImagenValidador.Validar(bytes, _opt.MaxBytesImagen);
        if (error is not null) return ResultadoVision.Fallo(error);

        using var original = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (original.Empty())
            return ResultadoVision.Fallo("No se pudo leer la imagen. Usa JPG o PNG.");

        using var reducida = Reducir(original);
        var imagen = reducida ?? original;
        var lesiones = _lesiones.Analizar(imagen);

        IReadOnlyList<DeteccionVision> detecciones = Array.Empty<DeteccionVision>();
        if (_clasificador.Disponible)
        {
            detecciones = _clasificador.Clasificar(imagen, _opt.TopK)
                .Where(d => d.Confianza >= _opt.ConfianzaMinima)
                .ToList();
        }

        return new ResultadoVision(
            true, "", detecciones, lesiones, _clasificador.Disponible,
            original.Width, original.Height, reloj.ElapsedMilliseconds);
    }

    private Mat? Reducir(Mat original)
    {
        var lado = Math.Max(original.Width, original.Height);
        if (_opt.MaxLadoPx <= 0 || lado <= _opt.MaxLadoPx) return null;
        var factor = (double)_opt.MaxLadoPx / lado;
        var salida = new Mat();
        Cv2.Resize(original, salida, new Size(
            Math.Max(1, (int)Math.Round(original.Width * factor)),
            Math.Max(1, (int)Math.Round(original.Height * factor))),
            0, 0, InterpolationFlags.Area);
        return salida;
    }
}