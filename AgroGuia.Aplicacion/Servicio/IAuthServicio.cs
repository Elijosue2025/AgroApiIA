using AgroGuia.Aplicacion.DTO.DTOS.Auth;

namespace AgroGuia.Aplicacion.Servicio;

public interface IAuthServicio
{
    Task<LoginResponseDto> RegistrarAsync(RegistroRequestDto request);

    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
}