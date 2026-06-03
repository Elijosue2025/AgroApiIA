using AgroGuia.Aplicacion.Servicio;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class JwtServiceImpl : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtServiceImpl(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerarToken(long idUsuario, string nombre, string email)
    {
        var key = Encoding.UTF8.GetBytes(
            _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey no configurado"));

        // Fallback a 480 minutos si no está configurado o es inválido
        int minutos = 480;
        var minutosConfig = _configuration["Jwt:ExpirationMinutes"];
        if (!string.IsNullOrWhiteSpace(minutosConfig) &&
            int.TryParse(minutosConfig, out int minutosParseados) &&
            minutosParseados > 0)
        {
            minutos = minutosParseados;
        }

        var ahora = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new Claim("IdUsuario", idUsuario.ToString()),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(ClaimTypes.Email, email)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = ahora,                          // ← explícito
            Expires = ahora.AddMinutes(minutos),        // ← siempre > NotBefore
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}