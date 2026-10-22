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

    // -------------------------------------------------------------------------
    // API nueva
    // -------------------------------------------------------------------------

    public async Task<ConversacionResumenDto> CrearAsync(long usuarioId, string? titulo)
    {
        var now = DateTime.UtcNow;
        var tituloFinal = string.IsNullOrWhiteSpace(titulo) ? "Nueva consulta" : titulo.Trim();

        // Preferir puerto nuevo; si aún no está, usar legacy del repo
        Conversaciones creada;
        try
        {
            creada = await _repo.CrearAsync(new Conversaciones
            {
                UsuarioId = usuarioId,
                Titulo = tituloFinal,
                FechaCreacion = now,
                FechaActualizacion = now,
                Activo = true
            });
        }
        catch (NotImplementedException)
        {
            creada = await _repo.ConversacionCrearAsync(usuarioId, tituloFinal);
        }

        return MapResumen(creada);
    }

    public async Task<IReadOnlyList<ConversacionResumenDto>> ListarMiasAsync(long usuarioId)
    {
        IReadOnlyList<Conversaciones> list;
        try
        {
            list = await _repo.ListarPorUsuarioAsync(usuarioId, soloActivas: true);
        }
        catch (NotImplementedException)
        {
            var legacy = await _repo.ConversacionObtenerPorUsuarioAsync(usuarioId);
            list = legacy.Where(c => c.Activo != false).ToList();
        }

        return list.Select(MapResumen).ToList();
    }

    public async Task<ConversacionDetalleDto?> ObtenerMiaAsync(long usuarioId, long conversacionId)
    {
        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId);
        if (c is null) return null;

        IReadOnlyList<Mensajes> msgs;
        try
        {
            msgs = await _repo.ListarMensajesAsync(conversacionId);
        }
        catch (NotImplementedException)
        {
            var conMsgs = await _repo.ConversacionObtenerConMensajesAsync(conversacionId);
            msgs = conMsgs?.Mensajes?.OrderBy(m => m.Fecha).ToList()
                   ?? (IReadOnlyList<Mensajes>)Array.Empty<Mensajes>();
        }

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

        try
        {
            var msgs = await _repo.ListarMensajesAsync(conversacionId, take);
            return msgs.Select(MapMensaje).ToList();
        }
        catch (NotImplementedException)
        {
            var conMsgs = await _repo.ConversacionObtenerConMensajesAsync(conversacionId);
            return conMsgs?.Mensajes?
                .OrderBy(m => m.Fecha)
                .Take(take)
                .Select(MapMensaje)
                .ToList()
                ?? (IReadOnlyList<MensajeDto>)Array.Empty<MensajeDto>();
        }
    }

    public async Task<bool> RenombrarMiaAsync(long usuarioId, long conversacionId, string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return false;

        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId, tracking: true);
        if (c is null) return false;

        c.Titulo = titulo.Trim();
        c.FechaActualizacion = DateTime.UtcNow;
        await _repo.ActualizarAsync(c);
        return true;
    }

    public async Task<bool> EliminarMiaAsync(long usuarioId, long conversacionId)
    {
        var c = await ObtenerSiEsMiaAsync(usuarioId, conversacionId, tracking: true);
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

    // -------------------------------------------------------------------------
    // Compatibilidad (controller / Swagger actuales)
    // -------------------------------------------------------------------------

    public async Task<ConversacionDTO> CrearConversacionAsync(long usuarioId, string titulo)
    {
        var resumen = await CrearAsync(usuarioId, titulo);
        // Recargar entidad mínima para mapear DTO completo
        var c = await _repo.ObtenerPorIdAsync(resumen.Id)
                ?? await _repo.ConversacionCrearAsync(usuarioId,
                    string.IsNullOrWhiteSpace(titulo) ? "Nueva consulta" : titulo.Trim());

        return MapearADTO(c.Id == resumen.Id ? c : new Conversaciones
        {
            Id = resumen.Id,
            UsuarioId = usuarioId,
            Titulo = resumen.Titulo,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = resumen.FechaActualizacion,
            Activo = true
        });
    }

    public async Task<List<ConversacionDTO>> ObtenerConversacionesUsuarioAsync(long usuarioId)
    {
        // Reutiliza listado nuevo y mapea al DTO viejo
        IReadOnlyList<Conversaciones> list;
        try
        {
            list = await _repo.ListarPorUsuarioAsync(usuarioId, soloActivas: true);
        }
        catch (NotImplementedException)
        {
            list = await _repo.ConversacionObtenerPorUsuarioAsync(usuarioId);
        }

        return list.Select(MapearADTO).ToList();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<Conversaciones?> ObtenerSiEsMiaAsync(
        long usuarioId, long conversacionId, bool tracking = false)
    {
        Conversaciones? c;
        try
        {
            c = await _repo.ObtenerPorIdAsync(conversacionId);
        }
        catch (NotImplementedException)
        {
            c = await _repo.ConversacionObtenerConMensajesAsync(conversacionId);
        }

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
        Fecha = (DateTime)m.Fecha,
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