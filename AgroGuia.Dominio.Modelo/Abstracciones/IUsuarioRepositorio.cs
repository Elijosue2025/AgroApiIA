using AgroGuia.Dominio.Modelo.Entidades;


namespace AgroGuia.Dominio.Modelo.Abstracciones
{
    public interface IUsuarioRepositorio
    {
        Task CrearUsuarioAsync(Usuarios usuario);
        Task<Usuarios?> ObtenerPorEmailAsync(string email);
        Task<Usuarios?> ObtenerPorIdAsync(long id);
        Task<List<Usuarios>> ObtenerTodosAsync();
        Task ActualizarUsuarioAsync(Usuarios usuario);
        Task EliminarUsuarioAsync(long id);
    }
}