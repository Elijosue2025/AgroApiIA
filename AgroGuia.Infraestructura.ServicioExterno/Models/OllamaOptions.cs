namespace AgroGuia.Infraestructura.ServicioExterno.Models; // o el namespace que ya uses

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
}
