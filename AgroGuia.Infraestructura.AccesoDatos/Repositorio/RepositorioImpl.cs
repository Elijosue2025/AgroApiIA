using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio
{
    public class RepositorioImpl<T> : IRepositorio<T> where T : class
    {
        protected readonly AgroGuiaIA_DBContext _context;
        protected readonly DbSet<T> _dbSet;

        public RepositorioImpl(AgroGuiaIA_DBContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T> GetByIdAsync(long id)
        {
            // FindAsync puede devolver null, pero la interfaz espera Task<T> (no Task<T?>).
            // Por lo tanto, lanzamos una excepción si no se encuentra la entidad.
            var entidad = await _dbSet.FindAsync(id);
            if (entidad == null)
                throw new InvalidOperationException($"Entidad de tipo {typeof(T).Name} con id {id} no encontrada.");
            return entidad;
        }

        public async Task AddAsync(T entidad)
        {
            await _dbSet.AddAsync(entidad);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entidad)
        {
            _dbSet.Update(entidad);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(long id)
        {
            var entidad = await GetByIdAsync(id);

            if (entidad != null)
            {
                _dbSet.Remove(entidad);
                await _context.SaveChangesAsync();
            }
        }
    }
}