// AgroGuia.Infraestructura.ServicioExterno/RAG/PromptBuilder.cs
using System.Text;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

public sealed class PromptBuilder
{
    public const string RespuestaSinInformacion =
        "No encontré esa información en los manuales técnicos disponibles. " +
        "Prueba a indicar el cultivo, la plaga o el problema con más detalle, " +
        "o consulta con un técnico agrónomo de tu zona.";

    public const string RespuestaErrorAsistente =
        "El asistente no está disponible en este momento. Intenta de nuevo en unos minutos.";

    private const string Sistema = """
Eres AgroGuía IA, asistente agronómico.
Responde solo con el CONTEXTO. No inventes plagas, productos ni dosis.
Usa el HISTORIAL solo para entender a qué cultivo, plaga o tema se refiere el usuario (referencias como "eso", "más métodos", "y el trips").
Si preguntan por un cultivo o plaga concreta, céntrate en eso.
Español claro y natural. Sintetiza; no copies ni repitas.
Si hay control: monitoreo, cultural, biológico y productos solo si están en el contexto.
Si no alcanza el contexto, dilo en una frase. Sin saludos ni relleno.
Si la pregunta es específica (una plaga, un producto o un manejo concreto):
- Desarrolla con más detalle: síntomas, monitoreo, umbrales si existen, pasos de control y precauciones.
- Usa solo el CONTEXTO; no inventes especies ni dosis.
- Organiza en párrafos o puntos claros (monitoreo, cultural, biológico, productos del contexto).
Si la pregunta es general, responde de forma completa pero sin alargarte de más.
""";

    private readonly RagOptions _opt;
    public PromptBuilder(IOptions<RagOptions> opt) => _opt = opt.Value;

    public IReadOnlyList<ContextoRecuperado> SeleccionarContexto(IReadOnlyList<ContextoRecuperado> contextos)
    {
        var elegidos = new List<ContextoRecuperado>();
        int total = 0;
        foreach (var c in contextos)
        {
            if (elegidos.Count > 0 && total + c.Texto.Length > _opt.ContextoMaxCaracteres) break;
            elegidos.Add(c);
            total += c.Texto.Length;
        }
        return elegidos;
    }

    /// <summary>
    /// historial: texto ya formateado (últimos turnos). Puede ser null/vacío en el primer mensaje.
    /// </summary>
    public ChatPrompt Construir(
        string pregunta,
        IReadOnlyList<ContextoRecuperado> contextos,
        string? historial = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(historial))
        {
            sb.Append("HISTORIAL DE ESTA CONVERSACIÓN (solo para resolver referencias; no inventes datos):\n");
            sb.Append(historial.Trim());
            sb.Append("\n\n");
        }

        sb.Append("CONTEXTO:\n\n");
        for (int i = 0; i < contextos.Count; i++)
        {
            var c = contextos[i];
            sb.Append('[').Append(i + 1).Append("] ").Append(c.Titulo);
            if (!string.IsNullOrWhiteSpace(c.Seccion)) sb.Append(" | ").Append(c.Seccion);
            if (c.Pagina is not null) sb.Append(" | pág. ").Append(c.Pagina);
            sb.Append('\n').Append(c.Texto).Append("\n\n");
        }

        sb.Append("PREGUNTA ACTUAL: ").Append(pregunta.Trim())
          .Append("\n\nResponde usando solo el CONTEXTO. Si la pregunta es corta, interprétala con el HISTORIAL. Sé concreto y sin repeticiones.");

        return new ChatPrompt(Sistema, sb.ToString());
    }

    public string RespuestaExtractiva(IReadOnlyList<ContextoRecuperado> contextos)
    {
        var sb = new StringBuilder("El asistente no pudo generar la respuesta. Esto es lo que indican los manuales:\n\n");
        foreach (var c in contextos.Take(2))
        {
            var t = c.Texto.Length > 600 ? c.Texto[..600].TrimEnd() + "..." : c.Texto;
            sb.Append("• ").Append(t).Append("\n  (").Append(c.Titulo).Append(")\n\n");
        }
        return sb.ToString().TrimEnd();
    }
}