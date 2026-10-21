using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.DTO.DTOS.Diagnostico
{
    public class DiagnosticoImagenResponseDto
    {
        public bool Exito { get; set; }

        public string ErrorMensaje { get; set; } = string.Empty;

        /// <summary>false si aún no hay modelo ONNX: en ese caso solo se entrega el análisis de color.</summary>
        public bool ModeloDisponible { get; set; }

        /// <summary>Frase lista para mostrar al agricultor.</summary>
        public string Resumen { get; set; } = string.Empty;

        /// <summary>Clases más probables según el modelo, de mayor a menor confianza.</summary>
        public List<DeteccionDto> Detecciones { get; set; } = new();

        /// <summary>Daño estimado por color de la hoja (no requiere modelo).</summary>
        public LesionesDto? Lesiones { get; set; }

        /// <summary>Fragmentos de los manuales sobre cómo manejar lo detectado.</summary>
        public List<RecomendacionDto> Recomendaciones { get; set; } = new();

        public string Advertencia { get; set; } = string.Empty;

        public long TiempoMs { get; set; }

        public DateTime FechaRespuesta { get; set; }
    }

    public class DeteccionDto
    {
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Cultivo { get; set; }

        /// <summary>"Sano" | "Enfermedad" | "Plaga" | "Desconocido"</summary>
        public string Tipo { get; set; } = string.Empty;

        /// <summary>Probabilidad entre 0 y 1.</summary>
        public double Confianza { get; set; }

        public string? Descripcion { get; set; }
    }

    public class LesionesDto
    {
        public bool TejidoDetectado { get; set; }
        public double PorcentajeAfectado { get; set; }
        public int NumeroManchas { get; set; }

        /// <summary>"Baja" | "Moderada" | "Alta" | "Indeterminada"</summary>
        public string Severidad { get; set; } = string.Empty;
    }

    public class RecomendacionDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string? Seccion { get; set; }
        public int? Pagina { get; set; }
        public string Texto { get; set; } = string.Empty;
    }
}