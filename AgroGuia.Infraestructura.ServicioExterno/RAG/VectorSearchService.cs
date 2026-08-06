using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using System.Text.Json;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public class VectorSearchService : IVectorSearchService
    {
        private readonly IEmbeddingRepositorio _embeddingRepositorio;
        private readonly SimilarityService _similarityService;

        public VectorSearchService(
            IEmbeddingRepositorio embeddingRepositorio,
            SimilarityService similarityService)
        {
            _embeddingRepositorio = embeddingRepositorio;
            _similarityService = similarityService;
        }

        public async Task<List<ChunkSimilaridad>> BuscarPorVectorAsync(
            List<float> vectorConsulta, int topK = 4)
        {
            // 1. Cargar todos los chunks activos con sus vectores
            var todosLosChunks = await _embeddingRepositorio
                .ObtenerChunksActivosConVectorAsync();

            if (todosLosChunks == null || !todosLosChunks.Any())
                return new List<ChunkSimilaridad>();

            // 2. Calcular cosine similarity para cada chunk
            var resultados = new List<ChunkSimilaridad>();

            foreach (var chunk in todosLosChunks)
            {
                if (string.IsNullOrWhiteSpace(chunk.VectorEmbedding))
                    continue;

                try
                {
                    var vectorChunk = JsonSerializer
                        .Deserialize<List<float>>(chunk.VectorEmbedding);

                    if (vectorChunk == null || vectorChunk.Count == 0)
                        continue;

                    double score = _similarityService
                        .CalcularCosineSimilarity(vectorConsulta, vectorChunk);

                    resultados.Add(new ChunkSimilaridad
                    {
                        Contenido = chunk.Contenido,
                        Score = score,
                        Cultivo = chunk.Cultivo ?? "General",
                        Tema = chunk.Tema ?? "General"
                    });
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"⚠️ Vector mal formado en chunk {chunk.Id}: {ex.Message}");
                }
            }

            // 3. Ordenar por score descendente y retornar top K
            return resultados
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();
        }
    }
}
