using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IEmbeddingService
    {
        Task<EmbeddingResponse> GenerarEmbeddingAsync(string texto);
        /// <summary>Usa el prefijo "search_query: " que exige nomic-embed-text.</summary>
        Task<EmbeddingResponse> GenerarEmbeddingConsultaAsync(string texto, CancellationToken ct = default);

        /// <summary>Usa "search_document: " y procesa por lotes (/api/embed). Devuelve un resultado por texto, en orden.</summary>
        Task<IReadOnlyList<EmbeddingResponse>> GenerarEmbeddingsDocumentosAsync(
            IReadOnlyList<string> textos, CancellationToken ct = default);

    }

}
