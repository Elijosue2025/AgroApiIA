using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones;

public interface ICatalogoTerminosRepositorio
{
    Task<List<CatalogoTerminos>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<CatalogoTerminos?> BuscarPorNombreYTipoAsync(string nombre, string tipo, CancellationToken ct = default);
    Task CrearAsync(CatalogoTerminos termino, CancellationToken ct = default);
}