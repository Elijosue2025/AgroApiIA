using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio;

public class ConversacionRepositorioImpl : IConversacionRepositorio
{
    private readonly AgroGuiaIA_DBContext _context;

    public ConversacionRepositorioImpl(AgroGuiaIA_DBContext context)
    {
        _context = context;
    }

    public async Task<Conversaciones> CrearAsync(Conversaciones entidad)
    {
        if (string.IsNullOrWhiteSpace(entidad.Titulo))
            entidad.Titulo = "Nueva Consulta Agronómica";

        if (entidad.FechaCreacion == default)
            entidad.FechaCreacion = DateTime.UtcNow;

        entidad.FechaActualizacion = DateTime.UtcNow;
        entidad.Activo = true;

        await _context.Conversaciones.AddAsync(entidad);
        await _context.SaveChangesAsync();
        return entidad;
    }

    public async Task<Conversaciones?> ObtenerPorIdAsync(long id)
    {
        return await _context.Conversaciones
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IReadOnlyList<Conversaciones>> ListarPorUsuarioAsync(
        long usuarioId, bool soloActivas = true)
    {
        var q = _context.Conversaciones
            .AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId);

        if (soloActivas)
            q = q.Where(x => x.Activo == true);

        return await q
            .OrderByDescending(x => x.FechaActualizacion)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Mensajes>> ListarMensajesAsync(
        long conversacionId, int take = 200)
    {
        if (take <= 0) take = 200;
        if (take > 500) take = 500;

        return await _context.Mensajes
            .AsNoTracking()
            .Where(m => m.ConversacionId == conversacionId)
            .OrderBy(m => m.Fecha)
            .Take(take)
            .ToListAsync();
    }

    public async Task ActualizarAsync(Conversaciones entidad)
    {
        entidad.FechaActualizacion = DateTime.UtcNow;
        _context.Conversaciones.Update(entidad);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Conversaciones>> AdminListarAsync(
        long? usuarioId,
        DateTime? desde,
        DateTime? hasta,
        string? tituloContiene,
        int take = 50)
    {
        if (take <= 0) take = 50;
        if (take > 100) take = 100;

        var q = _context.Conversaciones.AsNoTracking().AsQueryable();

        if (usuarioId is > 0)
            q = q.Where(x => x.UsuarioId == usuarioId.Value);

        if (desde.HasValue)
            q = q.Where(x => x.FechaActualizacion >= desde.Value);

        if (hasta.HasValue)
            q = q.Where(x => x.FechaActualizacion <= hasta.Value);

        if (!string.IsNullOrWhiteSpace(tituloContiene))
            q = q.Where(x => x.Titulo != null && x.Titulo.Contains(tituloContiene));

        return await q
            .OrderByDescending(x => x.FechaActualizacion)
            .Take(take)
            .ToListAsync();
    }

    public async Task MensajeGuardarAsync(Mensajes mensaje)
    {
        await _context.Mensajes.AddAsync(mensaje);
        await _context.SaveChangesAsync();
    }
}