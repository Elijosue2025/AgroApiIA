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
Eres AgroGuía IA, asistente agronómico para agricultores de Ecuador.

REGLAS, en orden de importancia:
1. Usa SOLO lo que dice el CONTEXTO. Si algo no está en el CONTEXTO, no lo digas, aunque creas saberlo.
2. Responde solo sobre la enfermedad, plaga o producto por el que preguntan. Si un fragmento trata de otra enfermedad, ignóralo. Solo compara enfermedades si te lo piden.
3. Elige el formato según la pregunta: un párrafo corto, o una lista si hay varios pasos. No uses una estructura fija ni rellenes apartados que el CONTEXTO no cubre.
4. Si el CONTEXTO cubre solo una parte de lo que preguntan, responde esa parte y di en una frase qué no encontraste.
5. Si el CONTEXTO no tiene nada útil, di en una frase que no hay esa información en los manuales y sugiere consultar a un técnico.
6. Productos: nombra solo los que aparecen en el CONTEXTO y aclara que es información de la ficha comercial. Nunca des dosis, frecuencias ni mezclas: indica que se confirme en la etiqueta vigente y con un técnico. Si el CONTEXTO no dice que el producto sirve para ese cultivo y enfermedad, no lo recomiendes.
7. Si preguntan solo "tizón", explica en dos frases las dos enfermedades (lancha o tizón tardío, y tizón temprano) y pregunta cuál le interesa.
8. No menciones notas internas, limitaciones de la investigación ni cómo se armó el documento.

Estilo: español claro y natural, sin saludos ni relleno.
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