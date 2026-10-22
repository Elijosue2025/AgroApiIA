using System.Security.Claims;
using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;
using AgroGuia.Aplicacion.Servicio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversacionController : ControllerBase
{
    private readonly IConversacionServicio _svc;

    public ConversacionController(IConversacionServicio svc)
    {
        _svc = svc;
    }

    private long Uid()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue("usuarioId");

        if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out var id))
            throw new UnauthorizedAccessException("Token sin usuarioId.");

        return id;
    }

    /// <summary>POST /api/Conversacion</summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearConversacionRequestDto? body)
        => Ok(await _svc.CrearAsync(Uid(), body?.Titulo));

    /// <summary>POST /api/Conversacion/crear (compat front viejo)</summary>
    [HttpPost("crear")]
    public async Task<IActionResult> CrearLegacy([FromBody] CrearConversacionRequestDto? body)
    {
        var uid = body?.UsuarioId > 0 ? body.UsuarioId : Uid();
        var titulo = string.IsNullOrWhiteSpace(body?.Titulo)
            ? "Nueva Consulta Agronómica"
            : body!.Titulo.Trim();

        var c = await _svc.CrearConversacionAsync(uid, titulo);
        return Ok(c);
    }

    /// <summary>GET /api/Conversacion</summary>
    [HttpGet]
    public async Task<IActionResult> Mias()
        => Ok(await _svc.ListarMiasAsync(Uid()));

    /// <summary>GET /api/Conversacion/usuario/{usuarioId}</summary>
    [HttpGet("usuario/{usuarioId:long}")]
    public async Task<IActionResult> PorUsuario(long usuarioId)
    {
        if (usuarioId != Uid()) return Forbid();
        return Ok(await _svc.ObtenerConversacionesUsuarioAsync(usuarioId));
    }

    /// <summary>GET /api/Conversacion/{id}</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id)
    {
        var d = await _svc.ObtenerMiaAsync(Uid(), id);
        return d is null ? NotFound() : Ok(d);
    }

    /// <summary>GET /api/Conversacion/{id}/mensajes</summary>
    [HttpGet("{id:long}/mensajes")]
    public async Task<IActionResult> Mensajes(long id)
        => Ok(await _svc.MensajesMiosAsync(Uid(), id));

    /// <summary>PATCH /api/Conversacion/{id}</summary>
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Renombrar(long id, [FromBody] ActualizarConversacionDto body)
    {
        if (string.IsNullOrWhiteSpace(body?.Titulo)) return BadRequest();
        var ok = await _svc.RenombrarMiaAsync(Uid(), id, body.Titulo.Trim());
        return ok ? NoContent() : NotFound();
    }

    /// <summary>DELETE /api/Conversacion/{id}</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id)
        => await _svc.EliminarMiaAsync(Uid(), id) ? NoContent() : NotFound();
}