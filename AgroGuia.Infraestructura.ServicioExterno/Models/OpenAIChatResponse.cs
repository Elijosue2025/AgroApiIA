namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public class OpenAIChatResponse
    {
        public bool Exito { get; set; }

        public string Respuesta { get; set; } = string.Empty;

        public string ErrorMensaje { get; set; } = string.Empty;

        public int TokensEntrada { get; set; }

        public int TokensSalida { get; set; }

        public int TokensTotales { get; set; }

        public decimal CostoEstimado { get; set; }
    }
}