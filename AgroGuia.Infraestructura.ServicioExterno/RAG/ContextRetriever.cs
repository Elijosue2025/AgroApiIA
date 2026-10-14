using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

/// <summary>
/// MEJORAS:
///  - UN solo embedding por pregunta (RagEngine y ContextRetriever lo generaban cada uno).
///  - Ya no devuelve strings de error ("No se encontró información...") que terminaban
///    dentro del prompt como si fueran contexto real. Devuelve lista vacía.
///  - El fallback textual SQL LIKE se reemplazó por BM25 en memoria (misma búsqueda híbrida
///    sin vector), así que ya no depende de IConversacionRepositorio.
///  - Expansión de vecinos (parent-child sin cambiar el esquema): el acierto se localiza con el chunk
///    pequeño y al LLM se le da el chunk + vecinos de la misma sección, desduplicando párrafos.
/// </summary>
public sealed class ContextRetriever
{
    private readonly IEmbeddingService _embeddings;
    private readonly IVectorSearchService _search;
    private readonly IVectorIndex _index;
    private readonly RagOptions _opt;
    private readonly ILogger<ContextRetriever> _log;

    public ContextRetriever(IEmbeddingService embeddings, IVectorSearchService search, IVectorIndex index,
        IOptions<RagOptions> opt, ILogger<ContextRetriever> log)
    {
        _embeddings = embeddings; _search = search; _index = index; _opt = opt.Value; _log = log;
    }

    public async Task<IReadOnlyList<ContextoRecuperado>> ObtenerContextoAsync(
        string pregunta, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta)) return Array.Empty<ContextoRecuperado>();

        float[]? vector = null;
        var emb = await _embeddings.GenerarEmbeddingConsultaAsync(pregunta, ct);
        if (emb.Exito) vector = emb.Vector;
        else _log.LogWarning("Embedding de la pregunta falló ({E}); se usa solo búsqueda por palabras clave", emb.ErrorMensaje);

        var aciertos = await _search.BuscarAsync(pregunta, vector, _opt.TopK, ct);
        if (aciertos.Count == 0) return Array.Empty<ContextoRecuperado>();

        var snap = await _index.ObtenerAsync(ct);
        var cubiertos = new HashSet<(string, int)>();
        var resultado = new List<ContextoRecuperado>();

        foreach (var h in aciertos)
        {
            if (cubiertos.Contains((h.Archivo, h.Indice))) continue;   // ya incluido por otra expansión

            var vecinos = snap.PorArchivo.TryGetValue(h.Archivo, out var doc)
                ? doc.Where(c => Math.Abs(c.Indice - h.Indice) <= _opt.VecinosContexto && c.Seccion == h.Seccion).ToList()
                : new List<IndexedChunk>();

            var vistos = new HashSet<string>();
            var parrafos = new List<string>();
            foreach (var v in vecinos.Count > 0 ? vecinos.Select(x => x.Contenido) : new[] { h.Contenido })
                foreach (var p in v.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                    if (vistos.Add(p)) parrafos.Add(p);

            foreach (var v in vecinos) cubiertos.Add((v.Archivo, v.Indice));
            cubiertos.Add((h.Archivo, h.Indice));

            resultado.Add(new ContextoRecuperado(
                string.Join("\n\n", parrafos), h.Titulo, h.Seccion, h.Pagina, h.Score));
        }
        return resultado;
    }
}
