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
    private readonly OllamaOptions _opt;
    private readonly ILogger<OllamaChatService> _log;

    public OllamaChatService(
        IOllamaApiClient client,
        IOptions<OllamaOptions> options,
        ILogger<OllamaChatService> log)
    {
        _client = client;
        _opt = options.Value;
        _log = log;
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

            // Elige 3B o 7B según el modo
            var modelo = ResolverModelo(prompt.ModoModelo);
            var apiClient = ObtenerApiClient(modelo);

            _log.LogInformation(
                "Ollama chat modelo={Model} modo={Modo}",
                modelo,
                prompt.ModoModelo ?? "default");

            // Una sola generación (evita 2 llamadas system + user)
            var mensaje = string.IsNullOrWhiteSpace(prompt.Sistema)
                ? prompt.Usuario
                : $"[Instrucciones]\n{prompt.Sistema}\n\n[Consulta]\n{prompt.Usuario}";

            var chat = new Chat(apiClient);
            var sb = new System.Text.StringBuilder();

            await foreach (var fragmento in chat.SendAsync(mensaje).WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(fragmento))
                    sb.Append(fragmento);
            }

            var texto = sb.ToString().Trim();

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
            _log.LogError(ex, "Error consultando Ollama");
            Console.WriteLine($"[Ollama Error] {ex.Message}");

            return new ChatCompletionResult
            {
                Exito = false,
                ErrorMensaje = ex.Message,
                Respuesta = "Lo siento, el asistente local no está disponible en este momento."
            };
        }
    }

    /// <summary>
    /// rapido  → ChatModelRapido (llama3.2:3b)
    /// preciso → ChatModelPreciso (qwen2.5:7b)
    /// null    → ChatModel (por defecto)
    /// </summary>
    private string ResolverModelo(string? modo)
    {
        if (string.Equals(modo, "rapido", StringComparison.OrdinalIgnoreCase))
            return _opt.ChatModelRapido ?? "llama3.2:3b";

        if (string.Equals(modo, "preciso", StringComparison.OrdinalIgnoreCase))
            return _opt.ChatModelPreciso ?? _opt.ChatModel ?? "qwen2.5:7b";

        return _opt.ChatModel ?? "qwen2.5:7b";
    }

    private OllamaApiClient ObtenerApiClient(string modelo)
    {
        if (_client is OllamaApiClient concrete)
        {
            concrete.SelectedModel = modelo;
            return concrete;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_opt.BaseUrl)
            ? "http://localhost:11434"
            : _opt.BaseUrl.TrimEnd('/');

        var api = new OllamaApiClient(new Uri(baseUrl + "/"));
        api.SelectedModel = modelo;
        return api;
    }
}