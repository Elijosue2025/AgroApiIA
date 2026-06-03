using AgroGuia.Aplicacion.DTO.DTOS.Chat;

namespace AgroGuia.Aplicacion.Servicio
{
    public interface IChatServicio
    {
        Task<ChatResponseDto> ProcesarConsultaAsync(ConsultaRequestDto request);
    }
}
