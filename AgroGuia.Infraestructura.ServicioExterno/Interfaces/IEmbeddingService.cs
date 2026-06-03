using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IEmbeddingService
    {
        Task<EmbeddingResponse> GenerarEmbeddingAsync(string texto);
    }
}