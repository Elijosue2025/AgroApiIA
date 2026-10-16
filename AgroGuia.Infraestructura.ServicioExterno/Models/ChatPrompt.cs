namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    /// <summary>
    /// El adaptador de chat solo envía; el prompt lo construye la lógica (PromptBuilder).
    /// ModoModelo: "rapido" | "preciso" | null (modelo por defecto de Ollama:ChatModel).
    /// </summary>
    public sealed record ChatPrompt(string Sistema, string Usuario, string? ModoModelo = null);
}
