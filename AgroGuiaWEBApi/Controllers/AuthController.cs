using AgroGuia.Aplicacion.DTO.DTOS.Auth;
using AgroGuia.Aplicacion.Servicio;
using AgroGuiaWEBApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthServicio _authServicio;

    public AuthController(IAuthServicio authServicio)
    {
        _authServicio = authServicio;
    }

    [HttpPost("registro")]
    public async Task<IActionResult> Registro(RegistroRequestDto request)
        => Ok(await _authServicio.RegistrarAsync(request));

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
        => Ok(await _authServicio.LoginAsync(request));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequestDto request)
        => Ok(await _authServicio.RefrescarAsync(request.RefreshToken));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authServicio.CerrarSesionAsync(User.GetUserId());
        return NoContent();
    }
}