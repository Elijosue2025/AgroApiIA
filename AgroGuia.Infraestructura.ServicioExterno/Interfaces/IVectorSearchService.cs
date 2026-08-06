using AgroGuia.Infraestructura.ServicioExterno.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IVectorSearchService
    {
        Task<List<ChunkSimilaridad>> BuscarPorVectorAsync(
            List<float> vectorConsulta,
            int topK = 4);
    }
}
