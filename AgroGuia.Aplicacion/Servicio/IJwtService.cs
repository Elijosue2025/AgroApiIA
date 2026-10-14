namespace AgroGuia.Aplicacion.Servicio;

public interface IJwtService
{
    int MinutosExpiracion { get; }
    int DiasRefreshToken { get; }

    string GenerarToken(long idUsuario, string nombre, string email, string rol);
    string GenerarRefreshToken();
    string HashRefreshToken(string refreshToken);
}