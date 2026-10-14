namespace AgroGuia.Infraestructura.ServicioExterno.Ollama
{
    /// <summary>Reemplaza a OllamaConfig (que estaba sin usar) y a las URLs/modelos hardcodeados.</summary>
    public sealed class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ChatModel { get; set; } = "llama3.2:3b";
        public string EmbeddingModel { get; set; } = "nomic-embed-text";
        /// <summary>Ventana de contexto. Si no se envía, Ollama usa 2048-4096 y trunca el prompt en silencio.</summary>
        public int NumCtx { get; set; } = 8192;
        public float Temperature { get; set; } = 0.2f;
        public int MaxTokens { get; set; } = 700;
        public int TimeoutSeconds { get; set; } = 300;
        public int EmbeddingBatchSize { get; set; } = 16;
    }
}
