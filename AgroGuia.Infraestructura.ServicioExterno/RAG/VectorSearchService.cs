using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Utilidades;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

/// <summary>
/// MEJORAS: búsqueda HÍBRIDA.
///  1) Ranking vectorial (coseno sobre vectores normalizados en memoria).
///  2) Ranking BM25 por palabras clave (nombres de plagas, productos, dosis: los embeddings los diluyen).
///  3) Fusión con Reciprocal Rank Fusion (no requiere calibrar escalas distintas).
///  4) Boost si el chunk es del cultivo mencionado en la pregunta.
///  5) UMBRAL de relevancia: si nada lo supera se devuelve vacío y NO se llama al LLM
///     (antes siempre se enviaban 3 chunks, aunque fueran irrelevantes, y un modelo de 3B los usaba para inventar).
/// Si el embedding de la pregunta falló, degrada a solo-palabras-clave en vez de fallar.
/// </summary>
public sealed class VectorSearchService : IVectorSearchService
{
    private readonly IVectorIndex _index;
    private readonly SimilarityService _sim;
    private readonly ICatalogoAgronomico _catalogo;
    private readonly RagOptions _opt;
    private readonly ILogger<VectorSearchService> _log;

    public VectorSearchService(IVectorIndex index, SimilarityService sim, ICatalogoAgronomico catalogo,
        IOptions<RagOptions> opt, ILogger<VectorSearchService> log)
    {
        _index = index; _sim = sim; _catalogo = catalogo; _opt = opt.Value; _log = log;
    }

    public async Task<IReadOnlyList<ChunkSimilaridad>> BuscarAsync(
    string pregunta, float[]? vectorConsulta, int topK, CancellationToken ct = default)
    {
        var snap = await _index.ObtenerAsync(ct);
        var todos = snap.Chunks;
        if (todos.Count == 0 || string.IsNullOrWhiteSpace(pregunta))
            return Array.Empty<ChunkSimilaridad>();

        // ---- Filtro por cultivo ANTES del ranking (precisión) ----
        var cultivoPregunta = _catalogo.DetectarCultivo(pregunta);
        IReadOnlyList<IndexedChunk> chunks = todos;

        if (cultivoPregunta is not null)
        {
            var filtrados = todos
                .Where(c =>
                    string.Equals(c.Cultivo, cultivoPregunta, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Cultivo, "General", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Si hay suficientes del cultivo (+ General), rankeamos solo esos
            if (filtrados.Count >= Math.Max(topK, 3))
            {
                chunks = filtrados;
                _log.LogInformation(
                    "Filtro cultivo '{Cultivo}': {N}/{Total} chunks",
                    cultivoPregunta, filtrados.Count, todos.Count);
            }
            else
            {
                _log.LogInformation(
                    "Filtro cultivo '{Cultivo}' dejó solo {N} chunks; se usa índice completo",
                    cultivoPregunta, filtrados.Count);
            }
        }

        int n = chunks.Count;

        // Mapa: índice local en 'chunks' → hace falta para BM25 sobre subconjunto
        // Recalculamos BM25 solo sobre los candidatos filtrados
        float[]? cos = null;
        if (vectorConsulta is { Length: > 0 })
        {
            var q = _sim.Normalizar(vectorConsulta);
            cos = new float[n];
            for (int i = 0; i < n; i++)
                cos[i] = chunks[i].Vector.Length == q.Length
                    ? _sim.ProductoPunto(q, chunks[i].Vector)
                    : 0f;
        }

        var terminos = TextoUtil.Tokenizar(pregunta).Distinct().ToList();
        var bm25 = Bm25Sobre(chunks, snap.Df, snap.Chunks.Count, snap.LongitudPromedio, terminos);

        int c = _opt.CandidatosPorRanking;
        var rankVec = cos is null
            ? new List<int>()
            : Enumerable.Range(0, n).OrderByDescending(i => cos[i]).Take(c).ToList();
        var rankKw = Enumerable.Range(0, n).Where(i => bm25[i] > 0)
                               .OrderByDescending(i => bm25[i]).Take(c).ToList();

        var fusion = new Dictionary<int, double>();
        for (int r = 0; r < rankVec.Count; r++)
            fusion[rankVec[r]] = fusion.GetValueOrDefault(rankVec[r]) + 1.0 / (_opt.RrfK + r + 1);
        for (int r = 0; r < rankKw.Count; r++)
            fusion[rankKw[r]] = fusion.GetValueOrDefault(rankKw[r]) + 1.0 / (_opt.RrfK + r + 1);

        var posKw = rankKw.Select((idx, r) => (idx, r)).ToDictionary(x => x.idx, x => x.r);

        if (cos is not null)
            _log.LogInformation(
                "Mejor coseno: {S:F3} (umbral {U:F2})",
                rankVec.Count > 0 ? cos[rankVec[0]] : 0, _opt.MinScore);

        bool EsRelevante(int i)
        {
            bool kwTop = posKw.TryGetValue(i, out var p) && p < _opt.RangoKeywordRelevante;
            if (cos is null) return kwTop;
            return cos[i] >= _opt.MinScore || (kwTop && cos[i] >= _opt.MinScore * 0.85f);
        }

        return fusion
            .Select(kv =>
            {
                var ch = chunks[kv.Key];
                // Boost extra si coincide exacto (General no se boostea igual)
                bool mismoCultivo = cultivoPregunta is not null &&
                    string.Equals(ch.Cultivo, cultivoPregunta, StringComparison.OrdinalIgnoreCase);
                return (Idx: kv.Key, Score: kv.Value * (mismoCultivo ? _opt.BoostCultivo : 1.0));
            })
            .Where(x => EsRelevante(x.Idx))
            .OrderByDescending(x => x.Score)
            .Take(topK) // TopK se respeta (precisión: 5 en tu appsettings)
            .Select(x =>
            {
                var ch = chunks[x.Idx];
                return new ChunkSimilaridad
                {
                    Id = ch.Id,
                    Archivo = ch.Archivo,
                    Indice = ch.Indice,
                    Titulo = ch.Titulo,
                    Seccion = ch.Seccion,
                    Pagina = ch.Pagina,
                    Contenido = ch.Contenido,
                    Cultivo = ch.Cultivo,
                    Tema = ch.Tema,
                    Score = cos is null ? 0 : cos[x.Idx],
                    ScoreFusion = x.Score
                };
            })
            .ToList();
    }

    /// <summary>
    /// BM25 solo sobre la lista de candidatos (p. ej. ya filtrada por cultivo).
    /// </summary>
    private static double[] Bm25Sobre(
        IReadOnlyList<IndexedChunk> chunks,
        IReadOnlyDictionary<string, int> dfGlobal,
        int totalGlobal,
        double longitudPromedio,
        IReadOnlyList<string> terminos)
    {
        var scores = new double[chunks.Count];
        if (terminos.Count == 0) return scores;

        const double k1 = 1.2, b = 0.75;

        foreach (var t in terminos)
        {
            // df y N tienen que ser del MISMO corpus (el índice completo). Antes N era el tamaño del subconjunto
            // filtrado por cultivo: con df global 200 y subconjunto de 50, N = max(50, 200) = 200 y el IDF
            // salía ~0.0025 (el término valía casi cero) en vez de ~0.69 con N = 400.
            if (!dfGlobal.TryGetValue(t, out var df) || df <= 0) continue;
            double N = Math.Max(totalGlobal, df);
            double idf = Math.Log(1 + (N - df + 0.5) / (df + 0.5));

            for (int i = 0; i < scores.Length; i++)
            {
                var ch = chunks[i];
                if (!ch.Tf.TryGetValue(t, out var tf)) continue;
                double norm = tf + k1 * (1 - b + b * ch.LongitudTokens / Math.Max(1, longitudPromedio));
                scores[i] += idf * tf * (k1 + 1) / norm;
            }
        }
        return scores;
    }
}
