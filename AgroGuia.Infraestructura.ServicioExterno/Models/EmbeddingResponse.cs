namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public class EmbeddingResponse
    {
        public bool Exito { get; set; }

        public List<float> Vector { get; set; } = new();

        public string ErrorMensaje { get; set; } = string.Empty;
    }
}