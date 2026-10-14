using System;
using System.Collections.Generic;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed class VectorIndexSnapshot
    {
        public static readonly VectorIndexSnapshot Vacio = new();
        public IReadOnlyList<IndexedChunk> Chunks { get; init; } = Array.Empty<IndexedChunk>();
        public IReadOnlyDictionary<string, int> Df { get; init; } = new Dictionary<string, int>();
        public double LongitudPromedio { get; init; } = 1;
        public IReadOnlyDictionary<string, IReadOnlyList<IndexedChunk>> PorArchivo { get; init; }
            = new Dictionary<string, IReadOnlyList<IndexedChunk>>();
    }
}
