using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;

namespace AgroGuia.Infraestructura.ServicioExterno.Embeddings
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private const string OLLAMA_URL = "http://localhost:11434/api/embeddings";

        public OllamaEmbeddingService(IMemoryCache cache)
        {
            _cache = cache;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        public async Task<EmbeddingResponse> GenerarEmbeddingAsync(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return new EmbeddingResponse
                {
                    Exito = false,
                    ErrorMensaje = "El texto está vacío"
                };

            // ✅ Clave de caché confiable con SHA256, no GetHashCode
            string cacheKey = $"emb_{ComputeHash(texto.Trim())}";

            if (_cache.TryGetValue(cacheKey, out EmbeddingResponse? cached) && cached != null)
            {
                Console.WriteLine("⚡ Embedding desde caché");
                return cached;
            }

            try
            {
                var requestBody = new
                {
                    model = "nomic-embed-text",
                    prompt = texto.Trim()
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(OLLAMA_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    // ❌ NO cachear errores
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = $"Ollama error {response.StatusCode}: {errorContent}"
                    };
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("embedding", out var embeddingElement))
                    // ❌ NO cachear errores
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = "No se encontró 'embedding' en la respuesta de Ollama"
                    };

                var vector = embeddingElement.EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToList();

                if (vector.Count == 0)
                    // ❌ NO cachear errores
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = "El vector vino vacío"
                    };

                Console.WriteLine($"✅ Embedding generado: {vector.Count} dimensiones");

                var resultado = new EmbeddingResponse
                {
                    Exito = true,
                    Vector = vector
                };

                // ✅ Solo cachear si fue exitoso
                _cache.Set(cacheKey, resultado, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
                    SlidingExpiration = TimeSpan.FromMinutes(30),
                    Size = 1
                });

                return resultado;
            }
            catch (HttpRequestException ex)
            {
                return new EmbeddingResponse
                {
                    Exito = false,
                    ErrorMensaje = $"No se pudo conectar con Ollama. ¿Está corriendo? Error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OllamaEmbedding Error] {ex.Message}");
                return new EmbeddingResponse
                {
                    Exito = false,
                    ErrorMensaje = ex.Message
                };
            }
        }

        // ✅ Hash confiable para la clave de caché
        private static string ComputeHash(string texto)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(texto));
            return Convert.ToHexString(bytes)[..16]; // Solo primeros 16 chars
        }
    }
}