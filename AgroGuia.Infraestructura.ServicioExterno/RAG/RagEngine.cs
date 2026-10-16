using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

/// <summary>
/// MEJORAS:
///  - Ya no genera el embedding (lo hace ContextRetriever, una sola vez).
///  - Sin contexto relevante → responde sin llamar al LLM (más rápido y sin alucinaciones).
///  - Si el LLM falla → respuesta extractiva marcada con ModoFallback, y ErrorMensaje con la causa real
///    (antes devolvía Exito=true con un texto genérico y escondía el error).
///  - Devuelve Fuentes (manual, sección, página, score) para mostrar en la UI.
///  - CancellationToken de punta a punta.
/// </summary>
public sealed class RagEngine : IRAGEngine
{
    private readonly ContextRetriever _retriever;
    private readonly PromptBuilder _prompts;
    private readonly IChatCompletionService _chat;
    private readonly ILogger<RagEngine> _log;

    public RagEngine(ContextRetriever retriever, PromptBuilder prompts, IChatCompletionService chat, ILogger<RagEngine> log)
    {
        _retriever = retriever; _prompts = prompts; _chat = chat; _log = log;
    }

    public async Task<RAGResponse> ProcesarConsultaAsync(string preguntaUsuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(preguntaUsuario))
            return new RAGResponse { Exito = false, ErrorMensaje = "La pregunta está vacía." };

        try
        {
            var recuperados = await _retriever.ObtenerContextoAsync(preguntaUsuario, ct);

            if (recuperados.Count == 0)
                return new RAGResponse
                {
                    Exito = true,
                    Pregunta = preguntaUsuario,
                    SinContexto = true,
                    Respuesta = PromptBuilder.RespuestaSinInformacion
                };

            var usados = _prompts.SeleccionarContexto(recuperados);
            var resultado = await _chat.GenerarAsync(_prompts.Construir(preguntaUsuario, usados), ct);

            var respuesta = new RAGResponse
            {
                Pregunta = preguntaUsuario,
                ChunksUtilizados = usados.Select(u => u.Texto).ToList(),
                Fuentes = usados.Select(u => new FuenteCitada(u.Titulo, u.Seccion, u.Pagina, u.Score)).ToList(),
                TokensEntrada = resultado.TokensEntrada,
                TokensSalida = resultado.TokensSalida
            };

            if (resultado.Exito)
            {
                respuesta.Exito = true;
                respuesta.Respuesta = resultado.Respuesta;
            }
            else
            {
                _log.LogWarning("LLM falló ({E}); respuesta extractiva", resultado.ErrorMensaje);
                respuesta.Exito = true;
                respuesta.ModoFallback = true;
                respuesta.Respuesta = _prompts.RespuestaExtractiva(usados);
                respuesta.ErrorMensaje = resultado.ErrorMensaje;
            }
            return respuesta;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error procesando consulta RAG");
            return new RAGResponse { Exito = false, Pregunta = preguntaUsuario, ErrorMensaje = ex.Message };
        }
    }
}