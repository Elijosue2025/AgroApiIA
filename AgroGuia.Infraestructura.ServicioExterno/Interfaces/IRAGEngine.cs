using System.Threading;
using System.Threading.Tasks;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IRAGEngine
    {
        Task<RAGResponse> ProcesarConsultaAsync(string preguntaUsuario, CancellationToken ct = default);
    }
}
