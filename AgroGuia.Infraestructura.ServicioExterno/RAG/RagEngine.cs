using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

public class RagEngine : IRAGEngine
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IOpenAIService _openAIService;
    private readonly ContextRetriever _contextRetriever;

    public RagEngine(
        IEmbeddingService embeddingService,
        IOpenAIService openAIService,
        ContextRetriever contextRetriever)
    {
        _embeddingService = embeddingService;
        _openAIService = openAIService;
        _contextRetriever = contextRetriever;
    }

    public async Task<RAGResponse> ProcesarConsultaAsync(
        string preguntaUsuario)
    {
        try
        {
            var embeddingPregunta =
                await _embeddingService
                    .GenerarEmbeddingAsync(preguntaUsuario);

            if (!embeddingPregunta.Exito)
            {
                return new RAGResponse
                {
                    Exito = false,
                    ErrorMensaje = embeddingPregunta.ErrorMensaje
                };
            }

            var chunks =
                await _contextRetriever
                    .ObtenerChunksAsync(preguntaUsuario);

            var topChunks =
                chunks.Take(3).ToList();

            var respuesta =
                await _openAIService
                    .ObtenerRespuestaConRAGAsync(
                        preguntaUsuario,
                        topChunks);

            return new RAGResponse
            {
                Exito = respuesta.Exito,
                Pregunta = preguntaUsuario,
                Respuesta = respuesta.Respuesta,
                ChunksUtilizados = topChunks,
                ErrorMensaje = respuesta.ErrorMensaje
            };
        }
        catch (Exception ex)
        {
            return new RAGResponse
            {
                Exito = false,
                ErrorMensaje = ex.Message
            };
        }
    }
}