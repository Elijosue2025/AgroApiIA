using AgroGuia.Aplicacion.Servicio;
using AgroGuiaWEBApi.Extensions; // User.GetUserId()
using AgroGuiaWEBApi.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DiagnosticoController : ControllerBase
{
    private const long LimiteSolicitudBytes = 10 * 1024 * 1024;
    private readonly IDiagnosticoServicio _diagnosticoServicio;

    public DiagnosticoController(IDiagnosticoServicio diagnosticoServicio)
    {
        _diagnosticoServicio = diagnosticoServicio;
    }

    [HttpPost("analizar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteSolicitudBytes)]
    public async Task<IActionResult> Analizar(
        [FromForm] AnalizarImagenRequest request,
        CancellationToken ct)
    {
        if (request.Imagen is null || request.Imagen.Length == 0)
            return BadRequest(new { error = "La imagen es obligatoria (campo 'imagen')." });

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await request.Imagen.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        var uid = User.GetUserId();
        long? conversacionId = request.ConversacionId is > 0 ? request.ConversacionId : null;

        var respuesta = await _diagnosticoServicio.AnalizarImagenAsync(
            bytes,
            request.Cultivo,
            uid,
            conversacionId,
            ct);

        return respuesta.Exito ? Ok(respuesta) : BadRequest(respuesta);
    }

    [HttpGet("estado")]
    public IActionResult Estado() => Ok(_diagnosticoServicio.ObtenerEstado());

    [HttpGet("incidencias")]
    public async Task<IActionResult> MisIncidencias([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var list = await _diagnosticoServicio.ListarIncidenciasMiasAsync(User.GetUserId(), take, ct);
        return Ok(list);
    }

    [HttpGet("incidencias/{id:long}")]
    public async Task<IActionResult> Incidencia(long id, CancellationToken ct = default)
    {
        var item = await _diagnosticoServicio.ObtenerIncidenciaMiaAsync(User.GetUserId(), id, ct);
        return item is null ? NotFound() : Ok(item);
    }
}