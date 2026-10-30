using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones;

public interface IVisionIncidenciaRepositorio
{
    Task<VisionIncidencias> CrearAsync(VisionIncidencias entidad, CancellationToken ct = default);

    Task<VisionIncidencias?> ObtenerPorIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<VisionIncidencias>> ListarPorUsuarioAsync(
        long usuarioId, int take = 50, CancellationToken ct = default);
}