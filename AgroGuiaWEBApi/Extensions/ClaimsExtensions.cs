using System.Security.Claims;

namespace AgroGuiaWEBApi.Extensions;

public static class ClaimsExtensions
{
    public static long GetUserId(this ClaimsPrincipal user)
    {
        var valor = user.FindFirst("IdUsuario")?.Value
            ?? throw new UnauthorizedAccessException("Token sin IdUsuario");
        return long.Parse(valor);
    }
}