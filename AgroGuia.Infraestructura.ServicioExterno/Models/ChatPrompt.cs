namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    /// <summary>El adaptador de chat solo envía; el prompt lo construye la lógica (PromptBuilder).</summary>
    public sealed record ChatPrompt(string Sistema, string Usuario);
}
