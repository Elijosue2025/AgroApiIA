using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace AgroGuia.Infraestructura.ServicioExterno.Ollama;

public sealed class OllamaChatService : IChatCompletionService
{
    private readonly IOllamaApiClient _client;
    private readonly string _modelName;
    private readonly ILogger<OllamaChatService> _log;

    // Si registras IOllamaApiClient en DI (como en tu ServicioExternoRegistration):
    public OllamaChatService(
        IOllamaApiClient client,
        IOptions<OllamaOptions> options,
        ILogger<OllamaChatService> log)
    {
        _client = client;
        _modelName = options.Value.ChatModel ?? "llama3.2:3b";
        _log = log;

        if (_client is OllamaApiClient concrete)
            concrete.SelectedModel = _modelName;
    }

    // Constructor simple por si aún no inyectas IOllamaApiClient:
    public OllamaChatService()
    {
        var api = new OllamaApiClient(new Uri("http://localhost:11434"));
        api.SelectedModel = "llama3.2:3b";
        _client = api;
        _modelName = "llama3.2:3b";
        _log = null!;
    }

    public async Task<ChatCompletionResult> GenerarAsync(
        ChatPrompt prompt,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(prompt.Usuario))
            {
                return new ChatCompletionResult
                {
                    Exito = false,
                    ErrorMensaje = "El prompt de usuario está vacío."
                };
            }

            // Cliente concreto de OllamaSharp para el helper Chat
            var apiClient = _client as OllamaApiClient
                ?? new OllamaApiClient(new Uri("http://localhost:11434"))
                {
                    SelectedModel = _modelName
                };

            if (!string.IsNullOrWhiteSpace(_modelName))
                apiClient.SelectedModel = _modelName;

            var chat = new Chat(apiClient);

            // 1) System
            if (!string.IsNullOrWhiteSpace(prompt.Sistema))
            {
                await foreach (var _ in chat.SendAsAsync(ChatRole.System, prompt.Sistema)
                    .WithCancellation(ct))
                { }
            }

            // 2) User → respuesta
            var respuesta = new System.Text.StringBuilder();
            await foreach (var fragmento in chat.SendAsAsync(ChatRole.User, prompt.Usuario)
                .WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                respuesta.Append(fragmento);
            }

            var texto = respuesta.ToString().Trim();

            return new ChatCompletionResult
            {
                Exito = true,
                Respuesta = string.IsNullOrWhiteSpace(texto)
                    ? "No encontré información suficiente en los manuales técnicos sobre este tema."
                    : texto,
                TokensEntrada = 0,
                TokensSalida = 0
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Error consultando Ollama ({Model})", _modelName);
            Console.WriteLine($"[Ollama Error] {ex.Message}");

            return new ChatCompletionResult
            {
                Exito = false,
                ErrorMensaje = ex.Message,
                Respuesta = "Lo siento, el asistente local no está disponible en este momento."
            };
        }
    }
}