using AgroGuia.Aplicacion.DTO.DTOS.Auth;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.Extensions.Logging;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class AuthServicioImpl : IAuthServicio
{
    private const string CredencialesInvalidas = "Credenciales inválidas";

    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthServicioImpl> _logger;

    public AuthServicioImpl(
        IUsuarioRepositorio usuarioRepositorio,
        IJwtService jwtService,
        ILogger<AuthServicioImpl> logger)
    {
        _usuarioRepositorio = usuarioRepositorio
            ?? throw new ArgumentNullException(nameof(usuarioRepositorio));
        _jwtService = jwtService
            ?? throw new ArgumentNullException(nameof(jwtService));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LoginResponseDto> RegistrarAsync(RegistroRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NombreCompleto) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 6)
                return Falla("Datos de registro incompletos o contraseña demasiado corta");

            var existeUsuario = await _usuarioRepositorio.ObtenerPorEmailAsync(request.Email);
            if (existeUsuario != null)
                return Falla("El email ya está registrado");

            var usuario = new Usuarios
            {
                NombreCompleto = request.NombreCompleto.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Telefono = request.Telefono,
                Rol = "Agricultor",              // el rol nunca viene del cliente
                FechaRegistro = DateTime.UtcNow,
                Activo = true
            };

            await _usuarioRepositorio.CrearUsuarioAsync(usuario);

            return await EmitirSesionAsync(usuario, "Usuario registrado correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el registro de usuario");
            return Falla("No se pudo completar el registro");
        }
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        try
        {
            var usuario = await _usuarioRepositorio.ObtenerPorEmailAsync(request.Email);

            // Mismo mensaje en ambos casos: no revela qué correos existen
            if (usuario == null || !usuario.Activo)
                return Falla(CredencialesInvalidas);

            bool passwordCorrecta = BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash);
            if (!passwordCorrecta)
                return Falla(CredencialesInvalidas);

            usuario.UltimoAcceso = DateTime.UtcNow;   // se guarda dentro de EmitirSesionAsync

            return await EmitirSesionAsync(usuario, "Login exitoso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el login");
            return Falla("No se pudo iniciar sesión");
        }
    }

    public async Task<LoginResponseDto> RefrescarAsync(string refreshToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return Falla("Sesión expirada");

            var hash = _jwtService.HashRefreshToken(refreshToken);
            var usuario = await _usuarioRepositorio.ObtenerPorRefreshTokenAsync(hash);

            if (usuario == null ||
                usuario.RefreshTokenExpiry == null ||
                usuario.RefreshTokenExpiry < DateTime.UtcNow ||
                !usuario.Activo)
                return Falla("Sesión expirada");

            // Rotación: cada refresh emite uno nuevo y el anterior deja de servir
            return await EmitirSesionAsync(usuario, "Token renovado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al renovar el token");
            return Falla("No se pudo renovar la sesión");
        }
    }

    public async Task CerrarSesionAsync(long usuarioId)
    {
        var usuario = await _usuarioRepositorio.ObtenerPorIdAsync(usuarioId);
        if (usuario == null) return;

        usuario.RefreshToken = null;
        usuario.RefreshTokenExpiry = null;
        await _usuarioRepositorio.ActualizarUsuarioAsync(usuario);
    }

    // Genera access token + refresh token, guarda el hash del refresh y devuelve la sesión
    private async Task<LoginResponseDto> EmitirSesionAsync(Usuarios usuario, string mensaje)
    {
        string token = _jwtService.GenerarToken(
            usuario.Id, usuario.NombreCompleto, usuario.Email, usuario.Rol);

        string refreshToken = _jwtService.GenerarRefreshToken();

        usuario.RefreshToken = _jwtService.HashRefreshToken(refreshToken);
        usuario.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtService.DiasRefreshToken);
        await _usuarioRepositorio.ActualizarUsuarioAsync(usuario);

        return new LoginResponseDto
        {
            Exito = true,
            Mensaje = mensaje,
            UsuarioId = usuario.Id,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Rol = usuario.Rol,
            Token = token,
            RefreshToken = refreshToken,
            ExpiraEn = DateTime.UtcNow.AddMinutes(_jwtService.MinutosExpiracion)
        };
    }

    private static LoginResponseDto Falla(string mensaje) =>
        new LoginResponseDto { Exito = false, Mensaje = mensaje };
}