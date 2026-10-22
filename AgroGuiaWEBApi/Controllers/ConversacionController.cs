using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;
using AgroGuia.Aplicacion.Servicio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenCvSharp;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversacionController : ControllerBase
{
    private readonly IConversacionServicio _svc;
    public ConversacionController(IConversacionServicio svc) => _svc = svc;

    long Uid() => long.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException());

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearConversacionRequestDto? body)
    {
        var c = await _svc.CrearAsync(Uid(), body?.Titulo);
        return Ok(c);
    }

    [HttpGet]
    public async Task<IActionResult> Mias()
        => Ok(await _svc.ListarMiasAsync(Uid()));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id)
    {
        var d = await _svc.ObtenerMiaAsync(Uid(), id);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpGet("{id:long}/mensajes")]
    public async Task<IActionResult> Mensajes(long id)
        => Ok(await _svc.MensajesMiosAsync(Uid(), id));

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Renombrar(long id, [FromBody] ActualizarConversacionDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Titulo)) return BadRequest();
        var ok = await _svc.RenombrarMiaAsync(Uid(), id, body.Titulo.Trim());
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id)
        => await _svc.EliminarMiaAsync(Uid(), id) ? NoContent() : NotFound();
}