namespace AgroGuia.Infraestructura.ServicioExterno.Models;

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "qwen2.5:7b";
    public string ChatModelRapido { get; set; } = "llama3.2:3b";
    public string ChatModelPreciso { get; set; } = "qwen2.5:7b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 180;
    public float Temperature { get; set; } = 0.4f;
    public float TopP { get; set; } = 0.9f;
    public float RepeatPenalty { get; set; } = 1.15f;
    public int NumPredict { get; set; } = 350;

    /// <summary>
    /// Ventana de contexto. Con Rag:ContextoMaxCaracteres = 3500 el prompt ronda los 1500-2000 tokens:
    /// 4096 sobra y gasta mucha menos memoria que 8192.
    /// </summary>
    public int NumCtx { get; set; } = 4096;

    /// <summary>
    /// Cuánto conserva Ollama el modelo cargado ("30m", "24h"; "-1m" = para siempre). Por defecto lo descarga a los
    /// 5 minutos y la siguiente pregunta paga la carga completa.
    /// </summary>
    public string KeepAlive { get; set; } = "30m";

    /// <summary>Cuántos textos se envían por request a /api/embed.</summary>
    public int EmbeddingBatchSize { get; set; } = 16;
}
