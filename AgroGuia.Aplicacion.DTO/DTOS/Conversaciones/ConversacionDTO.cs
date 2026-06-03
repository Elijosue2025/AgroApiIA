namespace AgroGuia.Aplicacion.DTO.DTOS.Conversaciones
{
    public class ConversacionDTO
    {
        public long Id { get; set; }

        public long UsuarioId { get; set; }

        public string Titulo { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; }

        public DateTime? FechaActualizacion { get; set; }

        public bool Activo { get; set; }
    }
}
