using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;
using AgroGuia.Aplicacion.Servicio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConversacionController : ControllerBase
    {
        private readonly IConversacionServicio _conversacionServicio;

        public ConversacionController(IConversacionServicio conversacionServicio)
        {
            _conversacionServicio = conversacionServicio;
        }

        [HttpPost("crear")]
        public async Task<IActionResult> Crear([FromBody] CrearConversacionRequestDto request)
        {
            if (request?.UsuarioId <= 0)
                return BadRequest(new { error = "UsuarioId es requerido." });

            try
            {
                var conversacion = await _conversacionServicio
                    .CrearConversacionAsync(request.UsuarioId, request.Titulo ?? "Nueva Consulta Agronómica");

                return Ok(conversacion);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("usuario/{usuarioId}")]
        public async Task<IActionResult> ObtenerPorUsuario(long usuarioId)
        {
            if (usuarioId <= 0)
                return BadRequest(new { error = "UsuarioId inválido." });

            var lista = await _conversacionServicio.ObtenerConversacionesUsuarioAsync(usuarioId);
            return Ok(lista);
        }
    }
}