namespace AgroGuia.Infraestructura.ServicioExterno.OpenAI
{
    public class OpenAIConfig
    {
        public string ApiKey { get; set; } = string.Empty;

        public string ModeloChat { get; set; } = "gpt-4o-mini";

        public string ModeloEmbedding { get; set; } = "text-embedding-3-small";

        public int MaxTokens { get; set; } = 700;

        public float Temperature { get; set; } = 0.0f;
    }
}