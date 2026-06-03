using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones
{
    public interface IEmbeddingRepositorio
    {
        Task CrearChunkAsync(EmbeddingChunks chunk);

        Task<List<EmbeddingChunks>> ObtenerTodosAsync();

        Task<EmbeddingChunks?> ObtenerPorIdAsync(long id);

        Task<List<EmbeddingChunks>> BuscarPorTemaAsync(string tema);

        Task<List<EmbeddingChunks>> BuscarPorCultivoAsync(string cultivo);

        Task<List<EmbeddingChunks>> ObtenerActivosAsync();

        Task DesactivarChunkAsync(long id);
    }
}