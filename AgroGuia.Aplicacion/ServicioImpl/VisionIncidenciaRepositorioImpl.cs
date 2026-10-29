using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio;

public class VisionIncidenciaRepositorioImpl : IVisionIncidenciaRepositorio
{
    private readonly AgroGuiaIA_DBContext _ctx;

    public VisionIncidenciaRepositorioImpl(AgroGuiaIA_DBContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<VisionIncidencias> CrearAsync(VisionIncidencias entidad, CancellationToken ct = default)
    {
        entidad.FechaCreacion = DateTime.UtcNow;
        entidad.Activo = true;
        await _ctx.VisionIncidencias.AddAsync(entidad, ct);
        await _ctx.SaveChangesAsync(ct);
        return entidad;
    }

    public Task<VisionIncidencias?> ObtenerPorIdAsync(long id, CancellationToken ct = default)
        => _ctx.VisionIncidencias.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Activo, ct);

    public async Task<IReadOnlyList<VisionIncidencias>> ListarPorUsuarioAsync(
        long usuarioId, int take = 50, CancellationToken ct = default)
    {
        return await _ctx.VisionIncidencias.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId && x.Activo)
            .OrderByDescending(x => x.FechaCreacion)
            .Take(take <= 0 ? 50 : Math.Min(take, 200))
            .ToListAsync(ct);
    }

    public async Task SoftDeleteAsync(long id, long usuarioId, CancellationToken ct = default)
    {
        var row = await _ctx.VisionIncidencias
            .FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId && x.Activo, ct);
        if (row is null) return;
        row.Activo = false;
        await _ctx.SaveChangesAsync(ct);
    }
}

public interface IVisionIncidenciaRepositorio
{
}