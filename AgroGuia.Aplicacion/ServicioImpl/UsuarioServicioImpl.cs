using AgroGuia.Aplicacion.DTO.DTOS.Usuarios;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class UsuarioServicioImpl : IUsuarioServicio
{
    private readonly IUsuarioRepositorio _usuarioRepositorio;

    public UsuarioServicioImpl(IUsuarioRepositorio usuarioRepositorio)
    {
        _usuarioRepositorio = usuarioRepositorio;
    }

    public async Task<List<UsuarioDTO>> ObtenerUsuariosAsync()
    {
        var usuarios = await _usuarioRepositorio.ObtenerTodosAsync();
        return usuarios.Select(x => new UsuarioDTO
        {
            Id = x.Id,
            NombreCompleto = x.NombreCompleto,
            Email = x.Email,
            Telefono = x.Telefono ?? string.Empty,
            Rol = x.Rol,
            FechaRegistro = x.FechaRegistro ?? DateTime.MinValue, // ← sin cast peligroso
            Activo = x.Activo
        }).ToList();
    }

    public async Task<UsuarioDTO?> ObtenerUsuarioPorIdAsync(long id)
    {
        var usuario = await _usuarioRepositorio.ObtenerPorIdAsync(id);
        if (usuario == null) return null;

        return new UsuarioDTO
        {
            Id = usuario.Id,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Telefono = usuario.Telefono ?? string.Empty,
            Rol = usuario.Rol,
            FechaRegistro = usuario.FechaRegistro ?? DateTime.MinValue, // ← sin cast peligroso
            Activo = usuario.Activo
        };
    }
}