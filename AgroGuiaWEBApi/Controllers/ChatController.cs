using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using AgroGuiaWEBApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatServicio _chatServicio;
    private readonly IConversacionServicio _conversacionServicio;

    public ChatController(
        IChatServicio chatServicio,
        IConversacionServicio conversacionServicio)
    {
        _chatServicio = chatServicio;
        _conversacionServicio = conversacionServicio;
    }

    [HttpPost("consultar")]
    public async Task<IActionResult> Consultar([FromBody] ConsultaRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Consulta))
            return BadRequest(new { error = "La consulta es obligatoria." });

        var uid = User.GetUserId();      // del token, no del body
        request.UsuarioId = uid;

        if (request.ConversacionId <= 0)
        {
            var nueva = await _conversacionServicio
                .CrearConversacionAsync(uid, "Nueva Consulta Agronómica");
            request.ConversacionId = nueva.Id;
        }
        else if (await _conversacionServicio.ObtenerMiaAsync(uid, request.ConversacionId) is null)
        {
            return NotFound(new { error = "Conversación no encontrada." });
        }

        var respuesta = await _chatServicio.ProcesarConsultaAsync(request);
        return respuesta.Exito ? Ok(respuesta) : BadRequest(respuesta);
    }
}