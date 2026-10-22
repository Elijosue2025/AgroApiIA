using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.DTO.DTOS.Chat
{
    public class ConversacionDetalleDto
    {
        public long Id { get; set; }
        public string Titulo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public string? CultivoActual { get; set; }
        public List<MensajeDto> Mensajes { get; set; } = new();
    }
}
