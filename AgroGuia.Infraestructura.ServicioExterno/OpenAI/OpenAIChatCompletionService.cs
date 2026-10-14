using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace AgroGuia.Infraestructura.ServicioExterno.OpenAI;

/// <summary>
/// Reemplaza a OpenAIService. Implementa el mismo puerto que OllamaChatService (IChatCompletionService):
/// solo envía el prompt que construye PromptBuilder. Se activa con "Llm:Provider": "OpenAI".
/// Reutiliza tu OpenAIConfig y la sección "OpenAI" del appsettings (ApiKey, ModeloChat, MaxTokens, Temperature).
/// OJO: dentro de este namespace NO escribas "OpenAI.Algo": "OpenAI" se resolvería a este namespace,
/// no al paquete. Por eso se usan los tipos del SDK sin prefijo.
/// </summary>
public sealed class OpenAIChatCompletionService : IChatCompletionService
{
    private readonly ChatClient _client;
    private readonly OpenAIConfig _config;
    private readonly ILogger<OpenAIChatCompletionService> _log;

    public OpenAIChatCompletionService(IOptions<OpenAIConfig> options, ILogger<OpenAIChatCompletionService> log)
    {
        _config = options.Value;
        _log = log;

        if (string.IsNullOrWhiteSpace(_config.ApiKey))
            throw new InvalidOperationException(
                "OpenAI:ApiKey no está configurada. Usa: dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\"");

        _client = new OpenAIClient(_config.ApiKey).GetChatClient(_config.ModeloChat);
    }

    public async Task<ChatCompletionResult> GenerarAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        try
        {
            var mensajes = new List<ChatMessage>
            {
                new SystemChatMessage(prompt.Sistema),
                new UserChatMessage(prompt.Usuario)
            };
            var opciones = new ChatCompletionOptions
            {
                Temperature = _config.Temperature,
                MaxOutputTokenCount = _config.MaxTokens
            };

            var resultado = await _client.CompleteChatAsync(mensajes, opciones, ct);

            return new ChatCompletionResult
            {
                Exito = true,
                Respuesta = resultado.Value.Content[0].Text.Trim(),
                TokensEntrada = resultado.Value.Usage?.InputTokenCount ?? 0,
                TokensSalida = resultado.Value.Usage?.OutputTokenCount ?? 0
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error consultando OpenAI");
            return new ChatCompletionResult { Exito = false, ErrorMensaje = ex.Message };
        }
    }
}
