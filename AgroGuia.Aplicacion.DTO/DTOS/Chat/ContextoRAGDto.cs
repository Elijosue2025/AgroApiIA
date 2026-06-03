namespace AgroGuia.Aplicacion.DTO.DTOS.Chat
{
    public class ContextoRAGDto
    {
        public string Fuente { get; set; } = string.Empty;

        public string Tema { get; set; } = string.Empty;

        public string Cultivo { get; set; } = string.Empty;

        public string Contenido { get; set; } = string.Empty;

        public decimal Similitud { get; set; }
    }
}
