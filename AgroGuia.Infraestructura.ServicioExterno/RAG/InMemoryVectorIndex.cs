using System.Text.Json;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Utilidades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

/// <summary>
/// NUEVA. Antes, VectorSearchService leía de SQL y deserializaba el JSON de TODOS los vectores
/// en cada pregunta. En tu tabla VectorEmbedding es NVARCHAR(MAX) con ~768 floats (decenas de KB por
/// fila): ese SELECT ya tarda más de un minuto en SSMS para 1000 filas.
/// Aquí se carga UNA vez, se normalizan los vectores y se precalculan los términos para BM25.
/// Es singleton: usa IServiceScopeFactory porque el repositorio es scoped.
/// SQL Server 2022 (16.0) no tiene tipo VECTOR, así que el caché en memoria es la opción correcta
/// hasta unas decenas de miles de chunks.
/// </summary>
public sealed class InMemoryVectorIndex : IVectorIndex
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SimilarityService _sim;
    private readonly ILogger<InMemoryVectorIndex> _log;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile VectorIndexSnapshot? _snapshot;

    public InMemoryVectorIndex(IServiceScopeFactory scopes, SimilarityService sim, ILogger<InMemoryVectorIndex> log)
    {
        _scopes = scopes; _sim = sim; _log = log;
    }

    public void Invalidar() => _snapshot = null;

    public async Task<VectorIndexSnapshot> ObtenerAsync(CancellationToken ct = default)
    {
        var s = _snapshot;
        if (s is not null) return s;

        await _lock.WaitAsync(ct);
        try { return _snapshot ??= await CargarAsync(); }
        finally { _lock.Release(); }
    }

    private async Task<VectorIndexSnapshot> CargarAsync()
    {
        using var scope = _scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IEmbeddingRepositorio>();
        var entidades = await repo.ObtenerChunksActivosConVectorAsync()
                        ?? Enumerable.Empty<EmbeddingChunks>().ToList();

        var chunks = new List<IndexedChunk>();
        int legacy = 0, invalidos = 0;
        int? dim = null;

        foreach (var e in entidades)
        {
            if (string.IsNullOrWhiteSpace(e.VectorEmbedding)) continue;

            var meta = LeerMetadata(e.Metadata);
            if (meta.Version < DocumentLoader.DocumentLoaderService.VersionEsquema) { legacy++; continue; }

            float[]? v;
            try { v = JsonSerializer.Deserialize<float[]>(e.VectorEmbedding); }
            catch (JsonException) { invalidos++; continue; }

            if (v is null || v.Length == 0) { invalidos++; continue; }
            dim ??= v.Length;
            if (v.Length != dim) { invalidos++; continue; }

            var tokens = TextoUtil.Tokenizar($"{e.Titulo} {meta.Seccion} {e.Contenido}").ToList();
            var tf = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

            chunks.Add(new IndexedChunk(
                e.Id, e.Titulo ?? "", e.Contenido, e.Cultivo ?? "General", e.Tema ?? "General",
                meta.Archivo ?? e.Titulo ?? "", meta.Indice, meta.Seccion, meta.Pagina, meta.Hash,
                _sim.Normalizar(v), tf, tokens.Count));
        }

        if (legacy > 0)
            _log.LogWarning("{N} chunks de la ingesta antigua (sin prefijos de nomic) fueron ignorados. " +
                            "Desactívalos y vuelve a cargar los PDFs.", legacy);
        if (invalidos > 0)
            _log.LogWarning("{N} chunks con vector inválido o de otra dimensión fueron ignorados", invalidos);

        var df = new Dictionary<string, int>();
        foreach (var c in chunks)
            foreach (var t in c.Tf.Keys)
                df[t] = df.GetValueOrDefault(t) + 1;

        _log.LogInformation("Índice vectorial cargado: {N} chunks, dimensión {D}", chunks.Count, dim ?? 0);

        return new VectorIndexSnapshot
        {
            Chunks = chunks,
            Df = df,
            LongitudPromedio = chunks.Count == 0 ? 1 : Math.Max(1, chunks.Average(c => c.LongitudTokens)),
            PorArchivo = chunks.GroupBy(c => c.Archivo).ToDictionary(
                g => g.Key, g => (IReadOnlyList<IndexedChunk>)g.OrderBy(c => c.Indice).ToList())
        };
    }

    private sealed record MetaInfo(int Version, string? Archivo, int Indice, string? Seccion, int? Pagina, string? Hash);

    private static MetaInfo LeerMetadata(string? json)
    {
        var legacy = new MetaInfo(1, null, 0, null, null, null);
        if (string.IsNullOrWhiteSpace(json)) return legacy;
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return legacy;

            string? Str(string n) => r.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            int? Int(string n) => r.TryGetProperty(n, out var p) && p.TryGetInt32(out var i) ? i : null;

            return new MetaInfo(Int("version") ?? 1, Str("archivo"), Int("indice") ?? 0,
                                Str("seccion"), Int("pagina"), Str("archivoHash"));
        }
        catch (JsonException) { return legacy; }
    }
}
