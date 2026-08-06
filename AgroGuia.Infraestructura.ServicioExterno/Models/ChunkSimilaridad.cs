using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public class ChunkSimilaridad
    {
        public string Contenido { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Cultivo { get; set; } = string.Empty;
        public string Tema { get; set; } = string.Empty;
    }

}
