using System.Collections.Generic;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IChunker
    {
        IReadOnlyList<ChunkPreparado> Dividir(IReadOnlyList<BloqueTexto> bloques);
    }
}
