using System.Net.Http.Json;
using System.Text.Json;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Ollama;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.Embeddings;

/// <summary>
/// MEJORAS: HttpClient tipado (sin new HttpClient()), URL/modelo por configuración,
/// prefijos obligatorios de nomic-embed-text, endpoint por lotes /api/embed,
/// CancellationToken, ILogger y cero Console.WriteLine por cada embedding.
/// </summary>
public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private const string PrefijoDocumento = "search_document: ";
    private const string PrefijoConsulta = "search_query: ";

    private readonly HttpClient _http;
    private readonly OllamaOptions _opt;
    private readonly ILogger<OllamaEmbeddingService> _log;

    public OllamaEmbeddingService(HttpClient http, IOptions<OllamaOptions> opt, ILogger<OllamaEmbeddingService> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<EmbeddingResponse> GenerarEmbeddingConsultaAsync(string texto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto)) return EmbeddingResponse.Fallo("El texto está vacío");
        var r = await EmbedAsync(new[] { PrefijoConsulta + texto.Trim() }, ct);
        return r[0];
    }

    public async Task<IReadOnlyList<EmbeddingResponse>> GenerarEmbeddingsDocumentosAsync(
        IReadOnlyList<string> textos, CancellationToken ct = default)
    {
        var resultados = new List<EmbeddingResponse>(textos.Count);
        for (int i = 0; i < textos.Count; i += _opt.EmbeddingBatchSize)
        {
            var lote = textos.Skip(i).Take(_opt.EmbeddingBatchSize)
                             .Select(t => PrefijoDocumento + t.Trim()).ToList();
            resultados.AddRange(await EmbedAsync(lote, ct));
        }
        return resultados;
    }

    private async Task<EmbeddingResponse[]> EmbedAsync(IReadOnlyList<string> entradas, CancellationToken ct)
    {
        EmbeddingResponse[] FalloTotal(string msg) =>
            Enumerable.Range(0, entradas.Count).Select(_ => EmbeddingResponse.Fallo(msg)).ToArray();

        try
        {
            using var resp = await _http.PostAsJsonAsync(
                "api/embed", new { model = _opt.EmbeddingModel, input = entradas, truncate = true, keep_alive = _opt.KeepAlive }, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var cuerpo = await resp.Content.ReadAsStringAsync(ct);
                return FalloTotal($"Ollama respondió {(int)resp.StatusCode}: {cuerpo}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("embeddings", out var arr))
                return FalloTotal("La respuesta de Ollama no trae 'embeddings' (¿versión < 0.1.44?)");

            var lista = new List<EmbeddingResponse>();
            foreach (var e in arr.EnumerateArray())
            {
                var v = new float[e.GetArrayLength()];
                int k = 0;
                foreach (var x in e.EnumerateArray()) v[k++] = x.GetSingle();
                lista.Add(v.Length == 0 ? EmbeddingResponse.Fallo("Vector vacío") : EmbeddingResponse.Ok(v));
            }

            return lista.Count == entradas.Count
                ? lista.ToArray()
                : FalloTotal($"Se esperaban {entradas.Count} embeddings y llegaron {lista.Count}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "No se pudo conectar con Ollama");
            return FalloTotal($"No se pudo conectar con Ollama. ¿Está corriendo? {ex.Message}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error generando embeddings");
            return FalloTotal(ex.Message);
        }
    }

    Task<EmbeddingResponse> IEmbeddingService.GenerarEmbeddingAsync(string texto)
    {
        throw new NotImplementedException();
    }
}

