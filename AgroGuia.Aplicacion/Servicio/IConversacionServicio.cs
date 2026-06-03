// IConversacionServicio.cs — CORRECTO así:
using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;

namespace AgroGuia.Aplicacion.Servicio;

public interface IConversacionServicio
{
    Task<List<ConversacionDTO>> ObtenerConversacionesUsuarioAsync(long usuarioId);

    Task<ConversacionDTO> CrearConversacionAsync(long usuarioId, string titulo);
}