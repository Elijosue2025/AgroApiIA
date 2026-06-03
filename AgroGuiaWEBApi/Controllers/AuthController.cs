using AgroGuia.Aplicacion.DTO.DTOS.Auth;
using AgroGuia.Aplicacion.Servicio;
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
    public async Task<IActionResult> Registro(
        RegistroRequestDto request)
    {
        var resultado =
            await _authServicio.RegistrarAsync(request);

        return Ok(resultado);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequestDto request)
    {
        var resultado =
            await _authServicio.LoginAsync(request);

        return Ok(resultado);
    }
}