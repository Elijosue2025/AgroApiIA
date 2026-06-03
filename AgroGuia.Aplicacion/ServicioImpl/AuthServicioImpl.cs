using AgroGuia.Aplicacion.DTO.DTOS.Auth;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class AuthServicioImpl : IAuthServicio
{
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly IJwtService _jwtService;

    // ← Sin parámetro opcional, ambos obligatorios
    public AuthServicioImpl(
        IUsuarioRepositorio usuarioRepositorio,
        IJwtService jwtService)
    {
        _usuarioRepositorio = usuarioRepositorio
            ?? throw new ArgumentNullException(nameof(usuarioRepositorio));
        _jwtService = jwtService
            ?? throw new ArgumentNullException(nameof(jwtService));
    }

    public async Task<LoginResponseDto> RegistrarAsync(RegistroRequestDto request)
    {
        try
        {
            var existeUsuario = await _usuarioRepositorio
                .ObtenerPorEmailAsync(request.Email);

            if (existeUsuario != null)
                return new LoginResponseDto
                {
                    Exito = false,
                    Mensaje = "El email ya está registrado"
                };

            var usuario = new Usuarios
            {
                NombreCompleto = request.NombreCompleto,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Telefono = request.Telefono,
                Rol = "Agricultor",
                FechaRegistro = DateTime.UtcNow,
                Activo = true
            };

            await _usuarioRepositorio.CrearUsuarioAsync(usuario);

            // ← Generar token tras registro
            string token = _jwtService.GenerarToken(
                usuario.Id,
                usuario.NombreCompleto,
                usuario.Email);

            return new LoginResponseDto
            {
                Exito = true,
                Mensaje = "Usuario registrado correctamente",
                UsuarioId = usuario.Id,
                NombreCompleto = usuario.NombreCompleto,
                Email = usuario.Email,
                Rol = usuario.Rol,
                Token = token   // ← token incluido
            };
        }
        catch (Exception ex)
        {
            return new LoginResponseDto { Exito = false, Mensaje = ex.Message };
        }
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        try
        {
            var usuario = await _usuarioRepositorio
                .ObtenerPorEmailAsync(request.Email);

            if (usuario == null)
                return new LoginResponseDto
                {
                    Exito = false,
                    Mensaje = "Usuario no encontrado"
                };

            bool passwordCorrecta = BCrypt.Net.BCrypt.Verify(
                request.Password,
                usuario.PasswordHash);

            if (!passwordCorrecta)
                return new LoginResponseDto
                {
                    Exito = false,
                    Mensaje = "Contraseña incorrecta"
                };

            usuario.UltimoAcceso = DateTime.UtcNow;
            await _usuarioRepositorio.ActualizarUsuarioAsync(usuario);

            // ← Generar token en login también
            string token = _jwtService.GenerarToken(
                usuario.Id,
                usuario.NombreCompleto,
                usuario.Email);

            return new LoginResponseDto
            {
                Exito = true,
                Mensaje = "Login exitoso",
                UsuarioId = usuario.Id,
                NombreCompleto = usuario.NombreCompleto,
                Email = usuario.Email,
                Rol = usuario.Rol,
                Token = token   // ← token incluido
            };
        }
        catch (Exception ex)
        {
            return new LoginResponseDto { Exito = false, Mensaje = ex.Message };
        }
    }
}