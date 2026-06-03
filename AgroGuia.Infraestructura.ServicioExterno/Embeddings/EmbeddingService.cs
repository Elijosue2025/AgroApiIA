using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace AgroGuia.Infraestructura.ServicioExterno.Embeddings
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly OpenAIClient _client;
        private readonly OpenAIConfig _config;

        public EmbeddingService(IOptions<OpenAIConfig> options)
        {
            _config = options.Value;

            _client = new OpenAIClient(_config.ApiKey);
        }

        public async Task<EmbeddingResponse> GenerarEmbeddingAsync(string texto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto))
                {
                    return new EmbeddingResponse
                    {
                        Exito = false,
                        ErrorMensaje = "Texto vacío."
                    };
                }

                var embeddingClient =
                    _client.GetEmbeddingClient(_config.ModeloEmbedding);

                var result =
                    await embeddingClient.GenerateEmbeddingAsync(texto);

                var vector =
                    result.Value.ToFloats().ToArray().ToList();

                return new EmbeddingResponse
                {
                    Exito = true,
                    Vector = vector
                };
            }
            catch (Exception ex)
            {
                return new EmbeddingResponse
                {
                    Exito = false,
                    ErrorMensaje = ex.Message
                };
            }
        }
    }
}