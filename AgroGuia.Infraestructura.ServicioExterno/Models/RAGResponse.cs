namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public class RAGResponse
    {
        public bool Exito { get; set; }

        public string Pregunta { get; set; } = string.Empty;

        public string Respuesta { get; set; } = string.Empty;

        public List<string> ChunksUtilizados { get; set; } = new();

        public string ErrorMensaje { get; set; } = string.Empty;
    }
}