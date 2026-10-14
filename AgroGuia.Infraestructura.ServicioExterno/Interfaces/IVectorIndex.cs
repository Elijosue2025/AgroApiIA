using System.Threading;
using System.Threading.Tasks;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IVectorIndex
    {
        Task<VectorIndexSnapshot> ObtenerAsync(CancellationToken ct = default);
        void Invalidar();
    }
}
