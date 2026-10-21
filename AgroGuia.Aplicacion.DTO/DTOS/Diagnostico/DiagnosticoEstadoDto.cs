using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.DTO.DTOS.Diagnostico
{
    public class DiagnosticoEstadoDto
    {
        public bool ModeloDisponible { get; set; }

        /// <summary>Nombre del archivo del modelo (nunca la ruta del servidor).</summary>
        public string? Modelo { get; set; }

        public int NumeroEtiquetas { get; set; }

        public string? Detalle { get; set; }
    }
}