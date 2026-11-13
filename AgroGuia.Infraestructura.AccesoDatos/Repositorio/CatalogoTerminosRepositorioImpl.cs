using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio;

public class CatalogoTerminosRepositorioImpl : ICatalogoTerminosRepositorio
{
    private readonly AgroGuiaIA_DBContext _context;

    public CatalogoTerminosRepositorioImpl(AgroGuiaIA_DBContext context)
    {
        _context = context;
    }

    public async Task<List<CatalogoTerminos>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        return await _context.CatalogoTerminos
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Tipo)
            .ThenBy(x => x.Nombre)
            .ToListAsync(ct);
    }

    public async Task<CatalogoTerminos?> BuscarPorNombreYTipoAsync(string nombre, string tipo, CancellationToken ct = default)
    {
        return await _context.CatalogoTerminos
            .FirstOrDefaultAsync(x =>
                x.Nombre == nombre &&
                x.Tipo == tipo, ct);
    }

    public async Task CrearAsync(CatalogoTerminos termino, CancellationToken ct = default)
    {
        await _context.CatalogoTerminos.AddAsync(termino, ct);
        await _context.SaveChangesAsync(ct);
    }
}