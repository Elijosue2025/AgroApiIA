using System.Text;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

/// <summary>
/// MEJORAS: el prompt vivía duplicado dentro de OllamaChatService/OpenAIService y esta clase no se
/// usaba. Ahora es la ÚNICA fuente del prompt (lógica de aplicación, no de infraestructura):
///  - System prompt corto con reglas (un 3B rinde más con reglas cortas).
///  - Contexto numerado con manual, sección y página (permite citar).
///  - Presupuesto de caracteres para no exceder num_ctx.
///  - Respuesta sin información y respuesta extractiva de respaldo (antes era código muerto).
/// AJUSTE: reglas contra lo que falló en las pruebas (nombres inventados, "¡Tienes razón!" ante una afirmación
/// falsa del usuario). Se quitó "indica el manual al final": el modelo puede inventar el nombre del manual; las
/// fuentes reales viajan en RAGResponse.Fuentes y [n] corresponde a Fuentes[n-1].
/// </summary>
public sealed class PromptBuilder
{
    public const string RespuestaSinInformacion =
        "No encontré esta información en los manuales técnicos disponibles. " +
        "Te recomiendo consultar con un técnico agrónomo de tu zona, o reformular la pregunta " +
        "indicando el cultivo, la etapa y el problema.";

    private const string Sistema = """
        Eres AgroGuia IA, asesor agronómico para agricultores de la Sierra Norte del Ecuador (Carchi).
        Reglas:
        1. Responde en español sencillo y práctico, sin saludos ni elogios a la pregunta.
        2. Usa SOLO la información del CONTEXTO. No uses conocimiento general.
        3. Cada dato debe salir de un fragmento: cita su número al final, por ejemplo [2].
        4. Nunca inventes nombres de plagas, enfermedades, productos, dosis, fechas ni cifras. Si un nombre no aparece en el CONTEXTO, no lo menciones.
        5. Si el CONTEXTO responde solo una parte, responde esa parte e indica qué falta. Si no responde nada, di: "No encontré esta información en los manuales disponibles."
        6. Si el usuario afirma algo que contradice el CONTEXTO, no le des la razón: indica lo que dice el CONTEXTO.
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

    public ChatPrompt Construir(string pregunta, IReadOnlyList<ContextoRecuperado> contextos)
    {
        var sb = new StringBuilder("CONTEXTO:\n\n");
        for (int i = 0; i < contextos.Count; i++)
        {
            var c = contextos[i];
            sb.Append('[').Append(i + 1).Append("] Manual: ").Append(c.Titulo);
            if (!string.IsNullOrWhiteSpace(c.Seccion)) sb.Append(" | Sección: ").Append(c.Seccion);
            if (c.Pagina is not null) sb.Append(" | pág. ").Append(c.Pagina);
            sb.Append('\n').Append(c.Texto).Append("\n\n");
        }
        sb.Append("PREGUNTA: ").Append(pregunta.Trim())
          .Append("\n\nResponde usando solo el contexto. Si no alcanza, dilo.");
        return new ChatPrompt(Sistema, sb.ToString());
    }

    public string RespuestaExtractiva(IReadOnlyList<ContextoRecuperado> contextos)
    {
        var sb = new StringBuilder("El asistente no está disponible; esto es lo que dicen los manuales:\n\n");
        foreach (var c in contextos.Take(2))
        {
            var t = c.Texto.Length > 600 ? c.Texto[..600].TrimEnd() + "..." : c.Texto;
            sb.Append("• ").Append(t).Append("\n  (").Append(c.Titulo).Append(")\n\n");
        }
        return sb.ToString().TrimEnd();
    }
}
