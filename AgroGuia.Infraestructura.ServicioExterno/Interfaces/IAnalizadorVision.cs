using AgroGuia.Infraestructura.ServicioExterno.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    /// <summary>
    /// Puerto del módulo de visión por computador. La capa de aplicación solo conoce bytes de imagen
    /// y resultados tipados: OpenCV queda encapsulado en Infraestructura.ServicioExterno/Vision.
    /// </summary>
    public interface IAnalizadorVision
    {
        /// <summary>Analiza una foto (JPG/PNG/WebP/BMP) de una planta y devuelve detecciones y daño estimado.</summary>
        Task<ResultadoVision> AnalizarAsync(byte[] imagen, CancellationToken ct = default);

        /// <summary>Indica si el modelo ONNX y las etiquetas están cargados. Fuerza la carga si aún no ocurrió.</summary>
        EstadoVision ObtenerEstado();
    }
}
