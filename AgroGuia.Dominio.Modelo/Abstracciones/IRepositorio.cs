namespace AgroGuia.Dominio.Modelo.Abstracciones
{
    public interface IRepositorio<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T> GetByIdAsync(long id);
        Task AddAsync(T entidad);
        Task UpdateAsync(T entidad);
        Task DeleteAsync(long id);
    }
}
