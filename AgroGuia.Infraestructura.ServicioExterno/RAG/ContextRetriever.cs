using AgroGuia.Dominio.Modelo.Abstracciones;


using AgroGuia.Infraestructura.ServicioExterno.Interfaces;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public class ContextRetriever
    {
        private readonly IConversacionRepositorio _conversacionRepositorio;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorSearchService _vectorSearchService;

        public ContextRetriever(
            IConversacionRepositorio conversacionRepositorio,
            IEmbeddingService embeddingService,
            IVectorSearchService vectorSearchService)
        {
            _conversacionRepositorio = conversacionRepositorio;
            _embeddingService = embeddingService;
            _vectorSearchService = vectorSearchService;
        }

        /// <summary>
        /// Flujo principal: Embedding → Cosine Similarity → Top K chunks
        /// Fallback: búsqueda textual LIKE si Ollama no responde
        /// </summary>
        public async Task<List<string>> ObtenerChunksAsync(
            string preguntaUsuario, int topK = 4)
        {
            if (string.IsNullOrWhiteSpace(preguntaUsuario))
                return new List<string>();

            try
            {
                // 1. Generar embedding de la pregunta
                var embeddingResult = await _embeddingService
                    .GenerarEmbeddingAsync(preguntaUsuario);

                if (!embeddingResult.Exito)
                {
                    Console.WriteLine($"⚠️ Embedding falló: {embeddingResult.ErrorMensaje} → fallback textual");
                    return await FallbackTextualAsync(preguntaUsuario, topK);
                }

                // 2. Ranking vectorial por cosine similarity
                var chunksRankeados = await _vectorSearchService
                    .BuscarPorVectorAsync(embeddingResult.Vector, topK);

                if (chunksRankeados == null || !chunksRankeados.Any())
                {
                    Console.WriteLine("⚠️ Sin resultados vectoriales → fallback textual");
                    return await FallbackTextualAsync(preguntaUsuario, topK);
                }

                Console.WriteLine($"✅ {chunksRankeados.Count} chunks por similitud vectorial " +
                                  $"(top score: {chunksRankeados[0].Score:F3})");

                return chunksRankeados
                    .Select(c => c.Contenido)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ContextRetriever] {ex.Message} → fallback textual");
                return await FallbackTextualAsync(preguntaUsuario, topK);
            }
        }

        private async Task<List<string>> FallbackTextualAsync(
            string pregunta, int topK)
        {
            try
            {
                var chunks = await _conversacionRepositorio
                    .BuscarChunksRelevantesAsync(pregunta);

                if (chunks == null || !chunks.Any())
                    return new List<string>
                    {
                        "No se encontró información en las guías técnicas."
                    };

                Console.WriteLine($"✅ {Math.Min(chunks.Count, topK)} chunks por palabras clave (fallback)");
                return chunks.Take(topK).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Fallback Error] {ex.Message}");
                return new List<string>
                {
                    "Error al recuperar información de la base de conocimiento."
                };
            }
        }

        public async Task<List<string>> ObtenerChunksAvanzadoAsync(
            string preguntaUsuario, int topK = 5)
        {
            // Usa directamente el flujo vectorial con más resultados
            return await ObtenerChunksAsync(preguntaUsuario, topK);
        }
    }
}