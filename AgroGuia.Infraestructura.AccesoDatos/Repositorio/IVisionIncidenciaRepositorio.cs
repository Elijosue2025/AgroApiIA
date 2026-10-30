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
        take = take <= 0 ? 50 : Math.Min(take, 200);
        return await _ctx.VisionIncidencias.AsNoTracking()
            .Where(x => x.UsuarioId == usuarioId && x.Activo)
            .OrderByDescending(x => x.FechaCreacion)
            .Take(take)
            .ToListAsync(ct);
    }
}