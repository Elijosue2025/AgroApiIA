using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IVectorSearchService
    {
        /// <summary>Búsqueda híbrida (vector + palabras clave). vectorConsulta puede ser null si falló el embedding.</summary>
        Task<IReadOnlyList<ChunkSimilaridad>> BuscarAsync(
            string pregunta, float[]? vectorConsulta, int topK, CancellationToken ct = default);
    }
}
