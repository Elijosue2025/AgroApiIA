using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio
{
    public class EmbeddingRepositorioImpl
        : RepositorioImpl<EmbeddingChunks>,
          IEmbeddingRepositorio
    {
        private new readonly AgroGuiaIA_DBContext _context;

        public EmbeddingRepositorioImpl(AgroGuiaIA_DBContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task CrearChunkAsync(EmbeddingChunks chunk)
        {
            await _context.EmbeddingChunks.AddAsync(chunk);
            await _context.SaveChangesAsync();
        }

        public async Task<List<EmbeddingChunks>> ObtenerTodosAsync()
        {
            return await _context.EmbeddingChunks.ToListAsync();
        }

        public async Task<EmbeddingChunks?> ObtenerPorIdAsync(long id)
        {
            return await _context.EmbeddingChunks
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<EmbeddingChunks>> ObtenerActivosAsync()
        {
            return await _context.EmbeddingChunks
                .Where(x => x.Activo == true)
                .ToListAsync();
        }

        public async Task<List<EmbeddingChunks>> BuscarPorTemaAsync(string tema)
        {
            return await _context.EmbeddingChunks
                .Where(x => x.Tema != null && x.Tema.Contains(tema))
                .ToListAsync();
        }

        public async Task<List<EmbeddingChunks>> BuscarPorCultivoAsync(string cultivo)
        {
            return await _context.EmbeddingChunks
                .Where(x => x.Cultivo != null && x.Cultivo.Contains(cultivo))
                .ToListAsync();
        }

        public async Task DesactivarChunkAsync(long id)
        {
            var chunk = await _context.EmbeddingChunks
                .FirstOrDefaultAsync(x => x.Id == id);

            if (chunk == null)
                throw new Exception("Chunk no encontrado");

            chunk.Activo = false;
            _context.EmbeddingChunks.Update(chunk);
            await _context.SaveChangesAsync();
        }

        // ✅ Solo trae los campos necesarios para el ranking vectorial
        // No trae Metadata ni PalabrasClave para reducir el payload
        public async Task<List<EmbeddingChunks>> ObtenerChunksActivosConVectorAsync()
        {
            return await _context.EmbeddingChunks
                .Where(static c => c.Activo == true && c.VectorEmbedding != null)
                .Select(c => new EmbeddingChunks
                {
                    Id = c.Id,
                    Contenido = c.Contenido,
                    VectorEmbedding = c.VectorEmbedding,
                    Cultivo = c.Cultivo,
                    Tema = c.Tema
                })
                .AsNoTracking()
                .ToListAsync();
        }
    }
}