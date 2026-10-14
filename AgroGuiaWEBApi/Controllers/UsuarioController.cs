using AgroGuia.Aplicacion.Servicio;
using AgroGuiaWEBApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroGuiaWEBApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuarioController : ControllerBase
{
    private readonly IUsuarioServicio _usuarioServicio;

    public UsuarioController(IUsuarioServicio usuarioServicio)
    {
        _usuarioServicio = usuarioServicio;
    }

    // Solo administradores
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ObtenerTodos()
        => Ok(await _usuarioServicio.ObtenerUsuariosAsync());

    // Datos del usuario logueado (el id sale del token)
    [HttpGet("me")]
    public async Task<IActionResult> ObtenerActual()
    {
        var usuario = await _usuarioServicio.ObtenerUsuarioPorIdAsync(User.GetUserId());
        return usuario is null ? NotFound() : Ok(usuario);
    }
}