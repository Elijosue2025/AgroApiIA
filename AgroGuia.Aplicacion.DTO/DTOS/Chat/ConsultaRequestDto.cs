namespace AgroGuia.Aplicacion.DTO.DTOS.Chat
{
    public class ConsultaRequestDto
    {
        public long UsuarioId { get; set; }

        public long ConversacionId { get; set; }

        public string Consulta { get; set; } = string.Empty;
    }
}
