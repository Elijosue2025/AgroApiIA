using System.Text;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace AgroGuia.Infraestructura.ServicioExterno.Ollama;

/// <summary>
/// MEJORAS (eran los bugs de mayor impacto):
///  1) Ya NO trunca cada chunk a 500 caracteres (indexabas 1150 y el modelo veía menos de la mitad).
///  2) Ya NO usa chat.SendAsAsync(ChatRole.System, ...): cada llamada generaba una respuesta del
///     modelo que se descartaba (2 generaciones inútiles por consulta) y contaminaba el historial.
///     Ahora es UNA petición con [system, user].
///  3) Fija num_ctx (8192): por defecto Ollama usa un contexto pequeño y recorta el prompt sin avisar.
///  4) Temperatura y max tokens por configuración; devuelve tokens reales; advierte si el prompt
///     se acerca al límite de contexto.
///  5) Errores reales: Exito=false + ErrorMensaje (antes Exito=true con texto genérico).
///  6) Sin prompt aquí: el adaptador solo envía; PromptBuilder decide el contenido (DIP).
/// Verifica los nombres de ChatRequest/Message/RequestOptions con tu versión de OllamaSharp (5.x).
/// </summary>
public sealed class OllamaChatService : IChatCompletionService
{
    private readonly IOllamaApiClient _client;
    private readonly OllamaOptions _opt;
    private readonly ILogger<OllamaChatService> _log;

    public OllamaChatService(IOllamaApiClient client, IOptions<OllamaOptions> opt, ILogger<OllamaChatService> log)
    {
        _client = client; _opt = opt.Value; _log = log;
    }

    public async Task<ChatCompletionResult> GenerarAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        try
        {
            var request = new ChatRequest
            {
                Model = _opt.ChatModel,
                Stream = true,
                Messages = new List<Message>
                {
                    new(ChatRole.System, prompt.Sistema),
                    new(ChatRole.User, prompt.Usuario)
                },
                Options = new RequestOptions
                {
                    NumCtx = _opt.NumCtx,
                    Temperature = _opt.Temperature,
                    NumPredict = _opt.MaxTokens
                }
            };

            var sb = new StringBuilder();
            int tokensIn = 0, tokensOut = 0;

            await foreach (var item in _client.ChatAsync(request, ct))
            {
                if (item?.Message?.Content is { } fragmento) sb.Append(fragmento);
                if (item is ChatDoneResponseStream done)
                {
                    tokensIn = done.PromptEvalCount;
                    tokensOut = done.EvalCount;
                }
            }

            if (tokensIn >= _opt.NumCtx - _opt.MaxTokens)
                _log.LogWarning("Prompt de {T} tokens cerca del límite num_ctx={C}: puede haberse truncado",
                                tokensIn, _opt.NumCtx);

            return new ChatCompletionResult
            {
                Exito = true,
                Respuesta = sb.ToString().Trim(),
                TokensEntrada = tokensIn,
                TokensSalida = tokensOut
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error consultando el modelo de chat en Ollama");
            return new ChatCompletionResult { Exito = false, ErrorMensaje = ex.Message };
        }
    }
}
