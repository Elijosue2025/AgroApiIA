using AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgroGuia.API.Controllers
{
    [ApiController]
    [Route("api/documentos")]
    public class DocumentosController : ControllerBase
    {
        private const long MaxPdfBytes = 524_288_000;
        private readonly DocumentLoaderService _loader;
        private readonly DocumentosConfig _config;

        public DocumentosController(
            DocumentLoaderService loader,
            IOptions<DocumentosConfig> options)
        {
            _loader = loader;
            _config = options.Value;
        }

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

        [HttpPost("cargar-uno")]
        public async Task<IActionResult> CargarUno([FromBody] CargarManualRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada." });

            if (request is null || string.IsNullOrWhiteSpace(request.NombreArchivo))
                return BadRequest(new { error = "Debes indicar NombreArchivo." });

            var (exito, mensaje) = await _loader.CargarManualAsync(
                _config.RutaManuales, request.NombreArchivo, ct, request.AOpciones());

            if (!exito)
                return BadRequest(new { error = mensaje });

            return Ok(new { mensaje });
        }

        /// <summary>
        /// form-data: archivo, y opcionales cultivo, tema, maxChars, minChars, soloGuardar.
        /// Hasta 500 MB, el mismo orden que copiar el PDF a la carpeta local.
        /// </summary>
        [HttpPost("subir")]
        [RequestSizeLimit(MaxPdfBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxPdfBytes)]
        public async Task<IActionResult> SubirManual(
            IFormFile archivo,
            [FromForm] string? cultivo,
            [FromForm] string? tema,
            [FromForm] int? maxChars,
            [FromForm] int? minChars,
            [FromForm] bool soloGuardar = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_config.RutaManuales))
                return BadRequest(new { error = "La ruta de manuales no está configurada." });

            if (archivo is null || archivo.Length == 0)
                return BadRequest(new { error = "Debes enviar un archivo PDF." });

            if (!archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Solo se permiten archivos PDF." });

            var opciones = new OpcionesCargaManual
            {
                Cultivo = cultivo,
                Tema = tema,
                MaxChars = maxChars,
                MinChars = minChars,
                SoloGuardar = soloGuardar
            };

            await using var stream = archivo.OpenReadStream();
            var (exito, mensaje, nombreGuardado) = await _loader.SubirYCargarManualAsync(
                _config.RutaManuales, stream, archivo.FileName, ct, opciones);

            if (!exito)
                return BadRequest(new { error = mensaje, archivo = nombreGuardado });

            return Ok(new { mensaje, archivoGuardado = nombreGuardado });
        }
    }

    public class CargarManualRequest
    {
        public string NombreArchivo { get; set; } = string.Empty;
        public string? Cultivo { get; set; }
        public string? Tema { get; set; }
        public int? MaxChars { get; set; }
        public int? MinChars { get; set; }

        public OpcionesCargaManual AOpciones() => new()
        {
            Cultivo = Cultivo,
            Tema = Tema,
            MaxChars = MaxChars,
            MinChars = MinChars
        };
    }
}
