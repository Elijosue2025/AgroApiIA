using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using System.Text;
using System.Text.Json;

namespace AgroGuia.Infraestructura.ServicioExterno.Embeddings
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private const string OLLAMA_URL = "http://localhost:11434/api/embeddings";

        public OllamaEmbeddingService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60) // Aumentamos timeout
            };
        }

        public async Task<EmbeddingResponse> GenerarEmbeddingAsync(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return new EmbeddingResponse
                {
                    Exito = false,
                    ErrorMensaje = "El texto está vacío"
                };
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
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = $"Ollama respondió con error {response.StatusCode}: {errorContent}"
                    };
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("embedding", out var embeddingElement))
                {
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = "No se encontró el campo 'embedding' en la respuesta de Ollama"
                    };
                }

                var vector = embeddingElement.EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToList();

                if (vector.Count == 0)
                {
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = "El vector de embedding vino vacío"
                    };
                }

                Console.WriteLine($"Embedding generado correctamente: {vector.Count} dimensiones");

                return new EmbeddingResponse
                {
                    Exito = true,
                    Vector = vector
                };
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
    }
}