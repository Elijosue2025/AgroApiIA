using System.Diagnostics;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision;

/// <summary>
/// MODO SEGURO (prototipo chat + RAG):
/// No usa OpenCV nativo. Evita el cierre del proceso con código -1 (0xffffffff).
/// Cuando tengas modelo.onnx + OpenCvSharp4.runtime.win estable,
/// se podrá volver a la implementación con Cv2.ImDecode.
///
/// Flujo diagrama:
/// - Modelo no disponible → SinModelo / SinDeteccion
/// - DiagnosticoServicioImpl sigue con RAG + LLM y pide consultar técnico
/// </summary>
public sealed class AnalizadorVisionOpenCv : IAnalizadorVision
{
    private readonly IClasificadorImagen _clasificador;
    private readonly VisionOptions _opt;
    private readonly ILogger<AnalizadorVisionOpenCv> _log;

    public AnalizadorVisionOpenCv(
        IClasificadorImagen clasificador,
        IOptions<VisionOptions> opt,
        ILogger<AnalizadorVisionOpenCv> log)
    {
        _clasificador = clasificador;
        _opt = opt.Value;
        _log = log;
    }

    public EstadoVision ObtenerEstado()
    {
        var e = _clasificador.Estado;
        if (!e.ModeloDisponible)
        {
            return e with
            {
                Detalle = (e.Detalle ?? "") +
                    " Visión en modo seguro (sin OpenCV nativo). " +
                    "La API no clasifica la foto; el chat responde con RAG e indica consultar a un técnico."
            };
        }

        // Hay archivo de modelo, pero este analizador aún no ejecuta DNN nativo
        // para no tumbar el proceso hasta validar runtime.
        return new EstadoVision(
            ModeloDisponible: false,
            Modelo: e.Modelo,
            NumeroEtiquetas: e.NumeroEtiquetas,
            Detalle: "Hay archivo de modelo configurado, pero el análisis nativo OpenCV está desactivado " +
                     "en modo seguro. Actívalo cuando OpenCvSharp4.runtime.win esté instalado y probado.");
    }

    public Task<ResultadoVision> AnalizarAsync(byte[] imagen, CancellationToken ct = default)
    {
        return Task.FromResult(Analizar(imagen));
    }

    private ResultadoVision Analizar(byte[] bytes)
    {
        var sw = Stopwatch.StartNew();

        var error = ImagenValidador.Validar(bytes, _opt.MaxBytesImagen);
        if (error is not null)
            return ResultadoVision.Fallo(error);

        _log.LogInformation(
            "[Vision] Modo seguro: foto recibida ({Bytes} bytes). Sin OpenCV nativo → SinModelo/SinDeteccion.",
            bytes.Length);

        // Caso 2 del diagrama: no se identifica la afección (aún no hay pipeline nativo estable)
        return new ResultadoVision(
            Exito: true,
            ErrorMensaje: string.Empty,
            Detecciones: Array.Empty<DeteccionVision>(),
            Lesiones: null,
            ModeloDisponible: false,
            Ancho: 0,
            Alto: 0,
            TiempoMs: sw.ElapsedMilliseconds);
    }
}