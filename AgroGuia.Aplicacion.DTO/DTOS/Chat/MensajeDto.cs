using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.DTO.DTOS.Chat
{
    public class MensajeDto
    {
        public long Id { get; set; }
        public string Rol { get; set; }      // user | assistant
        public string Contenido { get; set; }
        public DateTime Fecha { get; set; }
        public int? Tokens { get; set; }
    }
}
