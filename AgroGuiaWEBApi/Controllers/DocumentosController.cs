using AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

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

        /// <summary>
        /// Lista los PDFs disponibles en la carpeta de manuales.
        /// </summary>
        [HttpGet("listar")]
        public async Task<IActionResult> ListarManuales()
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada." });

            var archivos = await _loader.ListarManualesAsync(_config.RutaManuales);
            return Ok(new
            {
                ruta = _config.RutaManuales,
                total = archivos.Count,
                manuales = archivos
            });
        }

        /// <summary>
        /// Carga TODOS los manuales de la carpeta (comportamiento anterior).
        /// </summary>
        [HttpPost("cargar")]
        public async Task<IActionResult> CargarTodos(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada." });

            try
            {
                int total = await _loader.CargarDocumentosDesdeCarpetaAsync(_config.RutaManuales, ct);
                return Ok(new
                {
                    mensaje = "Carga de manuales completada",
                    documentosProcesados = total,
                    rutaUtilizada = _config.RutaManuales
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al cargar los manuales", detalle = ex.Message });
            }
        }

        /// <summary>
        /// Carga UN manual concreto por nombre de archivo.
        /// Ejemplo: POST /api/documentos/cargar-uno  body: { "nombreArchivo": "3_Manual-BPA-Quinua.pdf" }
        /// </summary>
        [HttpPost("cargar-uno")]
        public async Task<IActionResult> CargarUno([FromBody] CargarManualRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada." });

            if (request is null || string.IsNullOrWhiteSpace(request.NombreArchivo))
                return BadRequest(new { error = "Debes indicar NombreArchivo." });

            var (exito, mensaje) = await _loader.CargarManualAsync(
                _config.RutaManuales, request.NombreArchivo, ct);

            if (!exito)
                return BadRequest(new { error = mensaje });

            return Ok(new { mensaje });
        }

        /// <summary>
        /// Sube un PDF nuevo a la carpeta y lo carga al RAG.
        /// form-data: archivo = (el PDF)
        /// </summary>
        [HttpPost("subir")]
        [RequestSizeLimit(50_000_000)] // 50 MB
        public async Task<IActionResult> SubirManual(IFormFile archivo, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada." });

            if (archivo is null || archivo.Length == 0)
                return BadRequest(new { error = "Debes enviar un archivo PDF." });

            if (!archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Solo se permiten archivos PDF." });

            await using var stream = archivo.OpenReadStream();
            var (exito, mensaje, nombreGuardado) = await _loader.SubirYCargarManualAsync(
                _config.RutaManuales, stream, archivo.FileName, ct);

            if (!exito)
                return BadRequest(new { error = mensaje, archivo = nombreGuardado });

            return Ok(new
            {
                mensaje,
                archivoGuardado = nombreGuardado
            });
        }
    }

    public class CargarManualRequest
    {
        public string NombreArchivo { get; set; } = string.Empty;
    }
}