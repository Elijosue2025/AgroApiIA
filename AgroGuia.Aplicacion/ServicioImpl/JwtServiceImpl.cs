using AgroGuia.Aplicacion.Servicio;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class JwtServiceImpl : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtServiceImpl(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int MinutosExpiracion => LeerEntero("Jwt:ExpirationMinutes", 60);
    public int DiasRefreshToken => LeerEntero("Jwt:RefreshTokenDays", 7);

    private int LeerEntero(string clave, int porDefecto) =>
        int.TryParse(_configuration[clave], out var valor) && valor > 0 ? valor : porDefecto;

    public string GenerarToken(long idUsuario, string nombre, string email, string rol)
    {
        var key = Encoding.UTF8.GetBytes(
            _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey no configurado"));

        var ahora = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new Claim("IdUsuario", idUsuario.ToString()),
            new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, rol)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = ahora,
            Expires = ahora.AddMinutes(MinutosExpiracion),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }

    public string GenerarRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    // En la BD se guarda el hash, nunca el refresh token en claro
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}