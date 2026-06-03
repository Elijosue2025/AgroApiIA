using AgroGuia.Dominio.Modelo.Abstracciones;



namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public class ContextRetriever
    {
        private readonly IConversacionRepositorio _conversacionRepositorio;
        private readonly SimilarityService _similarityService;

        public ContextRetriever(
            IConversacionRepositorio conversacionRepositorio,
            SimilarityService similarityService)
        {
            _conversacionRepositorio = conversacionRepositorio;
            _similarityService = similarityService;
        }

        /// <summary>
        /// Obtiene los chunks más relevantes para la consulta del usuario
        /// </summary>
        public async Task<List<string>> ObtenerChunksAsync(string preguntaUsuario, int topK = 4)
        {
            if (string.IsNullOrWhiteSpace(preguntaUsuario))
                return new List<string>();

            try
            {
                // 1. Búsqueda por palabras clave (método actual que ya tienes)
                var chunksPorPalabras = await _conversacionRepositorio
                    .BuscarChunksRelevantesAsync(preguntaUsuario);

                // 2. Si tienes embeddings implementados, puedes combinar con similitud semántica
                // Por ahora usamos la búsqueda existente + ordenamiento mejorado

                if (chunksPorPalabras == null || !chunksPorPalabras.Any())
                {
                    return new List<string>
                    {
                        "No se encontró información relevante en los manuales técnicos."
                    };
                }

                // Tomamos los mejores resultados
                return chunksPorPalabras.Take(topK).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ContextRetriever Error] {ex.Message}");
                return new List<string>
                {
                    "Ocurrió un error al buscar información en la base de conocimiento."
                };
            }
        }

        /// <summary>
        /// Versión avanzada (opcional) - combina búsqueda por texto + similitud
        /// </summary>
        public async Task<List<string>> ObtenerChunksAvanzadoAsync(string preguntaUsuario, int topK = 5)
        {
            try
            {
                var todosLosChunks = await _conversacionRepositorio
                    .ChunksBuscarRelevantesAsync(preguntaUsuario, topK: 20);

                if (todosLosChunks == null || !todosLosChunks.Any())
                    return new List<string>();

                // Aquí podrías implementar similitud semántica si tienes embeddings
                return todosLosChunks.Take(topK).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ContextRetriever Avanzado Error] {ex.Message}");
                return await ObtenerChunksAsync(preguntaUsuario, topK);
            }
        }
    }
}