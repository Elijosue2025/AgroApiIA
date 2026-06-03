using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class ConversacionServicioImpl : IConversacionServicio
{
    private readonly IConversacionRepositorio _conversacionRepositorio;

    public ConversacionServicioImpl(IConversacionRepositorio conversacionRepositorio)
    {
        _conversacionRepositorio = conversacionRepositorio
            ?? throw new ArgumentNullException(nameof(conversacionRepositorio));
    }

    public async Task<ConversacionDTO> CrearConversacionAsync(long usuarioId, string titulo)
    {
        var conversacion = await _conversacionRepositorio
            .ConversacionCrearAsync(usuarioId, titulo);

        return MapearADTO(conversacion);
    }

    public async Task<List<ConversacionDTO>> ObtenerConversacionesUsuarioAsync(long usuarioId)
    {
        var lista = await _conversacionRepositorio
            .ConversacionObtenerPorUsuarioAsync(usuarioId);

        return lista.Select(c => MapearADTO(c)).ToList();
    }

    private static ConversacionDTO MapearADTO(Conversaciones c) => new()
    {
        Id = c.Id,
        UsuarioId = c.UsuarioId,
        Titulo = c.Titulo ?? string.Empty,
        FechaCreacion = (DateTime)c.FechaCreacion,
        FechaActualizacion = c.FechaActualizacion,
        Activo = (bool)c.Activo
    };
}