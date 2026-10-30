using AgroGuia.Aplicacion.Servicio;
using AgroGuiaWEBApi.Extensions; // User.GetUserId()
using AgroGuiaWEBApi.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace AgroGuiaWEBApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DiagnosticoController : ControllerBase
{
    private const long LimiteSolicitudBytes = 10 * 1024 * 1024;
    private readonly IDiagnosticoServicio _diagnosticoServicio;
    private readonly ILogger<DiagnosticoController> _log;

    public DiagnosticoController(
        IDiagnosticoServicio diagnosticoServicio,
        ILogger<DiagnosticoController> log)
    {
        _diagnosticoServicio = diagnosticoServicio;
        _log = log;
    }

    long Uid()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(s, out var id) ? id : 0;
    }

    /// <summary>
    /// multipart/form-data: campo "imagen", opcional "cultivo", opcional "conversacionId"
    /// </summary>
    [HttpPost("analizar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteSolicitudBytes)]
    public async Task<IActionResult> Analizar(
        [FromForm] AnalizarImagenRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request.Imagen is null || request.Imagen.Length == 0)
                return BadRequest(new
                {
                    exito = false,
                    estado = "Error",
                    errorMensaje = "La imagen es obligatoria (campo 'imagen').",
                    resumen = "No se recibió ninguna imagen.",
                    respuestaAsistente = "No llegó ninguna foto. Vuelve a adjuntarla e inténtalo de nuevo."
                });

            byte[] bytes;
            await using (var ms = new MemoryStream())
            {
                await request.Imagen.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            long? conversacionId = request.ConversacionId is > 0
                ? request.ConversacionId
                : null;

            var respuesta = await _diagnosticoServicio.AnalizarImagenAsync(
                bytes,
                request.Cultivo,
                Uid(),
                conversacionId,
                ct);

            return respuesta.Exito ? Ok(respuesta) : BadRequest(respuesta);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error no controlado en Diagnostico/analizar");
            return StatusCode(500, new
            {
                exito = false,
                estado = "Error",
                errorMensaje = ex.Message,
                resumen = "No se pudo analizar la imagen.",
                respuestaAsistente =
                    "El servicio de visión falló al procesar la foto. " +
                    "No es un diagnóstico. Prueba otra imagen o consulta solo por texto. " +
                    "Si el problema continúa, reinicia la API y revisa OpenCvSharp (runtime.win).",
                advertencia =
                    "Resultado orientativo. No sustituye diagnóstico de laboratorio ni de un técnico."
            });
        }
    }

    [HttpGet("estado")]
    public IActionResult Estado()
    {
        try
        {
            return Ok(_diagnosticoServicio.ObtenerEstado());
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error en Diagnostico/estado");
            return Ok(new
            {
                modeloDisponible = false,
                modelo = (string?)null,
                numeroEtiquetas = 0,
                detalle = "No se pudo consultar el estado de visión: " + ex.Message
            });
        }
    }

    [HttpGet("incidencias")]
    public async Task<IActionResult> MisIncidencias(
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var list = await _diagnosticoServicio.ListarIncidenciasMiasAsync(Uid(), take, ct);
        return Ok(list);
    }

    [HttpGet("incidencias/{id:long}")]
    public async Task<IActionResult> Incidencia(long id, CancellationToken ct = default)
    {
        var item = await _diagnosticoServicio.ObtenerIncidenciaMiaAsync(Uid(), id, ct);
        return item is null ? NotFound() : Ok(item);
    }
}