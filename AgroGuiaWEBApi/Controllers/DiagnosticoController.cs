using AgroGuia.Aplicacion.Servicio;
using AgroGuiaWEBApi.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers
{
    /// <summary>Reconocimiento de plagas y enfermedades a partir de fotos (OpenCV).</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DiagnosticoController : ControllerBase
    {
        // Margen sobre Vision:MaxBytesImagen (8 MB por defecto) para los bytes del multipart. El límite
        // real de la imagen lo aplica el servicio de visión.
        private const long LimiteSolicitudBytes = 10 * 1024 * 1024;

        private readonly IDiagnosticoServicio _diagnosticoServicio;

        public DiagnosticoController(IDiagnosticoServicio diagnosticoServicio)
        {
            _diagnosticoServicio = diagnosticoServicio;
        }

        /// <summary>
        /// Analiza una foto. Enviar como multipart/form-data con el archivo en el campo "imagen"
        /// y, opcionalmente, "cultivo".
        /// </summary>
        [HttpPost("analizar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(LimiteSolicitudBytes)]
        public async Task<IActionResult> Analizar([FromForm] AnalizarImagenRequest request, CancellationToken ct)
        {
            if (request.Imagen is null || request.Imagen.Length == 0)
                return BadRequest(new { error = "La imagen es obligatoria (campo 'imagen')." });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await request.Imagen.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var respuesta = await _diagnosticoServicio.AnalizarImagenAsync(bytes, request.Cultivo, ct);

            return respuesta.Exito ? Ok(respuesta) : BadRequest(respuesta);
        }

        /// <summary>Indica si el modelo de reconocimiento está cargado (útil para verificar la instalación).</summary>
        [HttpGet("estado")]
        public IActionResult Estado() => Ok(_diagnosticoServicio.ObtenerEstado());
    }
}