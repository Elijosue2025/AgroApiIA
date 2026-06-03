namespace AgroGuia.Aplicacion.DTO.DTOS.Chat
{
    public class ChatResponseDto
    {
        public bool Exito { get; set; }

        public string Respuesta { get; set; } = string.Empty;

        public int TokensUsados { get; set; }

        public DateTime FechaRespuesta { get; set; }

        public string ErrorMensaje { get; set; } = string.Empty;
    }

}
