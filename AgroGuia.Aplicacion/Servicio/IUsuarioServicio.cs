using AgroGuia.Aplicacion.DTO.DTOS.Usuarios;

namespace AgroGuia.Aplicacion.Servicio
{
    public interface IUsuarioServicio
    {
        Task<List<UsuarioDTO>> ObtenerUsuariosAsync();

        Task<UsuarioDTO?> ObtenerUsuarioPorIdAsync(long id);
    }

}
