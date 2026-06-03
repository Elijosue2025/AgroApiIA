using AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgroGuia.API.Controllers
{
    [ApiController]
    [Route("api/documentos")]
    public class DocumentosController : ControllerBase
    {
        private readonly DocumentLoaderService _loader;
        private readonly DocumentosConfig _config;

        public DocumentosController(
            DocumentLoaderService loader,
            IOptions<DocumentosConfig> options)
        {
            _loader = loader;
            _config = options.Value;
        }

        [HttpPost("cargar")]
        //[Authorize(Roles = "Administrador")]   // Descomenta cuando tengas roles configurados
        public async Task<IActionResult> CargarManuales()
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada en appsettings.json" });

            try
            {
                int total = await _loader.CargarDocumentosDesdeCarpetaAsync(_config.RutaManuales);

                return Ok(new
                {
                    mensaje = "Carga de manuales completada exitosamente",
                    documentosProcesados = total,
                    rutaUtilizada = _config.RutaManuales
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Error al cargar los manuales",
                    detalle = ex.Message
                });
            }
        }
    }
}