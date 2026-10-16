using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.Ollama;

/// <summary>
/// Chat contra Ollama por HTTP directo (/api/chat, sin stream): UNA sola llamada al modelo.
///
///  - La versión anterior usaba el helper Chat de OllamaSharp y mandaba el prompt de sistema con
///    chat.SendAsAsync(ChatRole.System, ...): ese método ENVÍA el mensaje y hace que el modelo GENERE una respuesta,
///    así que cada pregunta costaba dos generaciones (una se tiraba). Con un modelo de 7B eso duplicaba la espera.
///  - Elige el modelo según ChatPrompt.ModoModelo ("rapido" | "preciso"); antes el modo se leía pero nadie lo usaba.
///  - Envía num_ctx, num_predict, temperature, top_p y repeat_penalty de OllamaOptions (antes se ignoraban).
///  - keep_alive evita que Ollama descargue el modelo entre preguntas.
///  - Registra dónde se va el tiempo: carga del modelo, procesar el prompt y generar (tokens/s).
///
/// Usa el HttpClient con nombre "ollama-chat" que ya registra ServicioExternoRegistration.
/// </summary>
public sealed class OllamaChatService : IChatCompletionService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _opt;
    private readonly ILogger<OllamaChatService> _log;

    public OllamaChatService(IHttpClientFactory factory, IOptions<OllamaOptions> options, ILogger<OllamaChatService> log)
    {
        _http = factory.CreateClient("ollama-chat");
        _opt = options.Value;
        _log = log;
    }

    private string ElegirModelo(string? modo)
    {
        var modelo = modo switch
        {
            "rapido" => _opt.ChatModelRapido,
            "preciso" => _opt.ChatModelPreciso,
            _ => _opt.ChatModel
        };
        return string.IsNullOrWhiteSpace(modelo) ? _opt.ChatModel : modelo;
    }

    public async Task<ChatCompletionResult> GenerarAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt.Usuario))
            return new ChatCompletionResult { Exito = false, ErrorMensaje = "El prompt de usuario está vacío." };

        var modelo = ElegirModelo(prompt.ModoModelo);

        try
        {
            var cuerpo = new
            {
                model = modelo,
                stream = false,
                keep_alive = _opt.KeepAlive,
                options = new
                {
                    temperature = _opt.Temperature,
                    top_p = _opt.TopP,
                    repeat_penalty = _opt.RepeatPenalty,
                    num_ctx = _opt.NumCtx,
                    num_predict = _opt.NumPredict
                },
                messages = new[]
                {
                    new { role = "system", content = prompt.Sistema },
                    new { role = "user", content = prompt.Usuario }
                }
            };

            var reloj = Stopwatch.StartNew();
            using var res = await _http.PostAsJsonAsync("api/chat", cuerpo, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _log.LogError("Ollama respondió {Status} con el modelo {Modelo}: {Cuerpo}", (int)res.StatusCode, modelo, json);
                return new ChatCompletionResult
                {
                    Exito = false,
                    ErrorMensaje = res.StatusCode == HttpStatusCode.NotFound
                        ? $"El modelo '{modelo}' no está descargado. Ejecuta: ollama pull {modelo}"
                        : $"Ollama respondió {(int)res.StatusCode}."
                };
            }

            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            var texto = raiz.GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";

            long Ns(string n) => raiz.TryGetProperty(n, out var v) && v.TryGetInt64(out var x) ? x : 0;
            int Cuenta(string n) => raiz.TryGetProperty(n, out var v) && v.TryGetInt32(out var x) ? x : 0;

            int entrada = Cuenta("prompt_eval_count");
            int salida = Cuenta("eval_count");
            double cargaMs = Ns("load_duration") / 1e6;
            double promptMs = Ns("prompt_eval_duration") / 1e6;
            double genMs = Ns("eval_duration") / 1e6;
            double tps = genMs > 0 ? salida / (genMs / 1000.0) : 0;

            _log.LogInformation(
                "Ollama {Modelo}: {Total} ms en total | carga del modelo {Carga:F0} ms | prompt {PromptMs:F0} ms ({PromptTok} tok) | " +
                "generación {GenMs:F0} ms ({GenTok} tok, {Tps:F1} tok/s)",
                modelo, reloj.ElapsedMilliseconds, cargaMs, promptMs, entrada, genMs, salida, tps);

            if (cargaMs > 2000)
                _log.LogWarning("El modelo {Modelo} tardó {S:F1} s en CARGARSE en memoria (no estaba cargado). " +
                                "Con varios modelos, Ollama descarga uno para cargar otro: revisa 'ollama ps' y la RAM libre.",
                                modelo, cargaMs / 1000);

            // Si el prompt ocupa casi toda la ventana, Ollama recorta el INICIO (se perdería el prompt de sistema).
            if (entrada >= _opt.NumCtx - _opt.NumPredict)
                _log.LogWarning("Prompt de {T} tokens con num_ctx {C}: probable recorte. Baja Rag:ContextoMaxCaracteres o sube Ollama:NumCtx.",
                                entrada, _opt.NumCtx);

            if (string.IsNullOrWhiteSpace(texto))
                return new ChatCompletionResult { Exito = false, ErrorMensaje = "El modelo devolvió una respuesta vacía." };

            return new ChatCompletionResult
            {
                Exito = true,
                Respuesta = texto,
                TokensEntrada = entrada,
                TokensSalida = salida
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            _log.LogError("Ollama ({Modelo}) no respondió en {S} s (Ollama:TimeoutSeconds)", modelo, _opt.TimeoutSeconds);
            return new ChatCompletionResult
            {
                Exito = false,
                ErrorMensaje = $"Ollama no respondió en {_opt.TimeoutSeconds} s con el modelo {modelo}. ¿Es muy pesado para este equipo?"
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error consultando Ollama ({Modelo})", modelo);
            return new ChatCompletionResult { Exito = false, ErrorMensaje = ex.Message };
        }
    }
}
