using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;

namespace AgroGuia.Aplicacion.Servicio;

public interface IConversacionServicio
{
    // ===== API nueva (gestión completa) =====
    Task<ConversacionResumenDto> CrearAsync(long usuarioId, string? titulo);
    Task<IReadOnlyList<ConversacionResumenDto>> ListarMiasAsync(long usuarioId);
    Task<ConversacionDetalleDto?> ObtenerMiaAsync(long usuarioId, long conversacionId);
    Task<IReadOnlyList<MensajeDto>> MensajesMiosAsync(long usuarioId, long conversacionId, int take = 200);
    Task<bool> RenombrarMiaAsync(long usuarioId, long conversacionId, string titulo);
    Task<bool> EliminarMiaAsync(long usuarioId, long conversacionId);
    Task<IReadOnlyList<ConversacionResumenDto>> AdminListarAsync(
        long? filtroUsuarioId, DateTime? desde, DateTime? hasta, string? q, int take = 50);

    // ===== Compatibilidad (lo que ya tienes en el controller) =====
    Task<ConversacionDTO> CrearConversacionAsync(long usuarioId, string titulo);
    Task<List<ConversacionDTO>> ObtenerConversacionesUsuarioAsync(long usuarioId);
}