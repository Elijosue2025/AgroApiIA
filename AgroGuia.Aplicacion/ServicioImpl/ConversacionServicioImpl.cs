using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.DTO.DTOS.Conversaciones;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class ConversacionServicioImpl : IConversacionServicio
{
    private readonly IConversacionRepositorio _repo;

    public ConversacionServicioImpl(IConversacionRepositorio conversacionRepositorio)
    {
        _repo = conversacionRepositorio
            ?? throw new ArgumentNullException(nameof(conversacionRepositorio));
    }

    public async Task<ConversacionResumenDto> CrearAsync(long usuarioId, string? titulo)
    {
        var now = DateTime.UtcNow;
        var creada = await _repo.CrearAsync(new Conversaciones
        {
            UsuarioId = usuarioId,
            Titulo = string.IsNullOrWhiteSpace(titulo) ? "Nueva consulta" : titulo.Trim(),
            FechaCreacion = now,
            FechaActualizacion = now,
            Activo = true
        });
        return MapResumen(creada);
    }

    public async Task<IReadOnlyList<ConversacionResumenDto>> ListarMiasAsync(long usuarioId)
    {
        var list = await _repo.ListarPorUsuarioAsync(usuarioId, soloActivas: true);
        return list.Select(MapResumen).ToList();
    }

    public async Task<ConversacionDetalleDto?> ObtenerMiaAsync(long usuarioId, long conversacionId)
    {
        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId);
        if (c is null) return null;

        var msgs = await _repo.ListarMensajesAsync(conversacionId);
        return new ConversacionDetalleDto
        {
            Id = c.Id,
            Titulo = c.Titulo ?? "",
            FechaCreacion = c.FechaCreacion ?? DateTime.MinValue,
            FechaActualizacion = c.FechaActualizacion ?? c.FechaCreacion ?? DateTime.MinValue,
            CultivoActual = c.CultivoActual,
            Mensajes = msgs.Select(MapMensaje).ToList()
        };
    }

    public async Task<IReadOnlyList<MensajeDto>> MensajesMiosAsync(
        long usuarioId, long conversacionId, int take = 200)
    {
        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId);
        if (c is null) return Array.Empty<MensajeDto>();

        var msgs = await _repo.ListarMensajesAsync(conversacionId, take);
        return msgs.Select(MapMensaje).ToList();
    }

    public async Task<bool> RenombrarMiaAsync(long usuarioId, long conversacionId, string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return false;

        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId);
        if (c is null) return false;

        c.Titulo = titulo.Trim();
        c.FechaActualizacion = DateTime.UtcNow;
        await _repo.ActualizarAsync(c);
        return true;
    }

    public async Task<bool> EliminarMiaAsync(long usuarioId, long conversacionId)
    {
        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId);
        if (c is null) return false;

        c.Activo = false;
        c.FechaActualizacion = DateTime.UtcNow;
        await _repo.ActualizarAsync(c);
        return true;
    }

    public async Task<IReadOnlyList<ConversacionResumenDto>> AdminListarAsync(
        long? filtroUsuarioId, DateTime? desde, DateTime? hasta, string? q, int take = 50)
    {
        var list = await _repo.AdminListarAsync(filtroUsuarioId, desde, hasta, q, take);
        return list.Select(MapResumen).ToList();
    }

    public async Task<ConversacionDTO> CrearConversacionAsync(long usuarioId, string titulo)
    {
        var resumen = await CrearAsync(usuarioId, titulo);
        var c = await _repo.ObtenerPorIdAsync(resumen.Id);
        return MapearADTO(c!);
    }

    public async Task<List<ConversacionDTO>> ObtenerConversacionesUsuarioAsync(long usuarioId)
    {
        var list = await _repo.ListarPorUsuarioAsync(usuarioId, soloActivas: true);
        return list.Select(MapearADTO).ToList();
    }

    private async Task<Conversaciones?> ObtenerSiEsMiaAsync(long usuarioId, long conversacionId)
    {
        var c = await _repo.ObtenerPorIdAsync(conversacionId);
        if (c is null || c.UsuarioId != usuarioId || c.Activo == false)
            return null;
        return c;
    }

    private static ConversacionResumenDto MapResumen(Conversaciones c) => new()
    {
        Id = c.Id,
        Titulo = c.Titulo ?? "",
        FechaActualizacion = c.FechaActualizacion ?? c.FechaCreacion ?? DateTime.MinValue,
        CultivoActual = c.CultivoActual
    };

    private static MensajeDto MapMensaje(Mensajes m) => new()
    {
        Id = m.Id,
        Rol = m.Rol ?? "",
        Contenido = m.Contenido ?? "",
        Fecha = m.Fecha ?? DateTime.MinValue,
        Tokens = m.Tokens
    };

    private static ConversacionDTO MapearADTO(Conversaciones c) => new()
    {
        Id = c.Id,
        UsuarioId = c.UsuarioId,
        Titulo = c.Titulo ?? string.Empty,
        FechaCreacion = c.FechaCreacion ?? DateTime.MinValue,
        FechaActualizacion = c.FechaActualizacion,
        Activo = c.Activo ?? true
    };
}