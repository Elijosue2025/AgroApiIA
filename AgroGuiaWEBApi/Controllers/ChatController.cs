using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers
{
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

            if (request.UsuarioId <= 0)
                return BadRequest(new { error = "UsuarioId inválido." });

            // Crear conversación automáticamente si no se envía
            if (request.ConversacionId <= 0)
            {
                var nueva = await _conversacionServicio
                    .CrearConversacionAsync(request.UsuarioId, "Nueva Consulta Agronómica");

                request.ConversacionId = nueva.Id;
            }

            var respuesta = await _chatServicio.ProcesarConsultaAsync(request);

            return respuesta.Exito ? Ok(respuesta) : BadRequest(respuesta);
        }
    }
}