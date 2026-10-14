using System.Collections.Generic;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed record IndexedChunk(
        long Id, string Titulo, string Contenido, string Cultivo, string Tema,
        string Archivo, int Indice, string? Seccion, int? Pagina, string? Hash,
        float[] Vector, IReadOnlyDictionary<string, int> Tf, int LongitudTokens);
}
