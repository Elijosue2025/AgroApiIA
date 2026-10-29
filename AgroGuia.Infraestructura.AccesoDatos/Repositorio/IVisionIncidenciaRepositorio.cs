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

    /// <summary>Historial del usuario (solo activas), más recientes primero.</summary>
    Task<IReadOnlyList<VisionIncidencias>> ListarPorUsuarioAsync(
        long usuarioId, int take = 50, CancellationToken ct = default);

    Task SoftDeleteAsync(long id, long usuarioId, CancellationToken ct = default);
}