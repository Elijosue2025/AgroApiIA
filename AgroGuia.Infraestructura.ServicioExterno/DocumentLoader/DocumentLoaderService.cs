using System.Security.Cryptography;
using System.Text.Json;
using System.Transactions;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Utilidades;
using Microsoft.Extensions.Logging;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>
/// Ahora solo ORQUESTA (extraer → limpiar → dividir → clasificar → vectorizar → guardar);
/// cada paso vive en su propia clase detrás de una interfaz (SRP + DIP).
/// MEJORAS:
///  - Idempotente: hash SHA-256 del archivo guardado en Metadata; si ya está cargado se omite.
///  - Cultivo/tema por chunk (el documento solo es respaldo).
///  - Embeddings por lotes con prefijo search_document + título y sección como contexto.
///  - Si falla un embedding NO se guarda el documento (antes se guardaban chunks con vector NULL
///    que quedaban invisibles para la búsqueda) y se reintenta en la próxima carga.
///  - Texto saneado para las columnas VARCHAR y limitado a su tamaño real.
///  - Invalida el índice en memoria al terminar.
/// AJUSTE: los INSERT de un documento van dentro de un TransactionScope (todo o nada). Sin esto, si fallaba el
/// chunk 40 de 80 quedaban 39 guardados y la siguiente carga veía el hash y daba el documento por cargado.
/// </summary>
public class DocumentLoaderService
{
    public const int VersionEsquema = 2;   // v1 = ingesta antigua (sin prefijos ni orden de lectura)
    private const string Fuente = "Manual Técnico";

    private readonly IEmbeddingRepositorio _repo;
    private readonly IEmbeddingService _embeddings;
    private readonly IPdfTextExtractor _extractor;
    private readonly ITextCleaner _cleaner;
    private readonly IChunker _chunker;
    private readonly ICatalogoAgronomico _catalogo;
    private readonly IVectorIndex _index;
    private readonly ILogger<DocumentLoaderService> _log;

    public DocumentLoaderService(
        IEmbeddingRepositorio repo, IEmbeddingService embeddings, IPdfTextExtractor extractor,
        ITextCleaner cleaner, IChunker chunker, ICatalogoAgronomico catalogo,
        IVectorIndex index, ILogger<DocumentLoaderService> log)
    {
        _repo = repo; _embeddings = embeddings; _extractor = extractor; _cleaner = cleaner;
        _chunker = chunker; _catalogo = catalogo; _index = index; _log = log;
    }

    public async Task<int> CargarDocumentosDesdeCarpetaAsync(string carpetaPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(carpetaPath))
            throw new DirectoryNotFoundException($"Carpeta no encontrada: {carpetaPath}");

        var pdfs = Directory.GetFiles(carpetaPath, "*.pdf");
        var snapshot = await _index.ObtenerAsync(ct);
        var hashes = snapshot.Chunks.Where(c => c.Hash != null).Select(c => c.Hash!).ToHashSet();
        var archivos = snapshot.Chunks.Select(c => c.Archivo).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _log.LogInformation("Iniciando carga de {N} PDFs", pdfs.Length);
        int cargados = 0;

        foreach (var pdf in pdfs)
        {
            ct.ThrowIfCancellationRequested();
            var archivo = Path.GetFileName(pdf);
            try
            {
                var hash = await HashAsync(pdf, ct);
                if (hashes.Contains(hash)) { _log.LogInformation("Sin cambios, se omite: {A}", archivo); continue; }

                if (archivos.Contains(archivo))
                {
                    _log.LogWarning("{A} cambió pero su versión anterior sigue activa. Desactívala antes: " +
                        "UPDATE dbo.EmbeddingChunks SET Activo = 0 WHERE JSON_VALUE(Metadata,'$.archivo') = '{A}'",
                        archivo, archivo);
                    continue;
                }

                if (await ProcesarPdfAsync(pdf, archivo, hash, ct)) { cargados++; hashes.Add(hash); }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogError(ex, "Error procesando {A}", archivo); }
        }

        if (cargados > 0) _index.Invalidar();
        _log.LogInformation("Carga completada. Documentos nuevos: {N}", cargados);
        return cargados;
    }

    private async Task<bool> ProcesarPdfAsync(string ruta, string archivo, string hash, CancellationToken ct)
    {
        var doc = _extractor.Extraer(ruta);
        var bloques = _cleaner.Limpiar(doc);
        var chunks = _chunker.Dividir(bloques);

        if (chunks.Count == 0)
        {
            _log.LogWarning("PDF sin contenido útil (¿escaneado?): {T}", doc.Titulo);
            return false;
        }

        var textoDoc = string.Join("\n", bloques.Select(b => b.Texto));
        var cultivoDoc = _catalogo.DetectarCultivo(textoDoc, doc.Titulo) ?? "General";
        var temaDoc = _catalogo.DetectarTema(textoDoc, doc.Titulo) ?? "General";

        var tituloLegible = doc.Titulo.Replace('_', ' ').Replace('-', ' ');
        var textosEmbedding = chunks.Select(c =>
            $"{tituloLegible}{(c.Seccion is null ? "" : " > " + c.Seccion)}\n{c.Contenido}").ToList();

        var embeddings = await _embeddings.GenerarEmbeddingsDocumentosAsync(textosEmbedding, ct);
        var fallos = embeddings.Count(e => !e.Exito);
        if (fallos > 0)
            throw new InvalidOperationException(
                $"Fallaron {fallos}/{chunks.Count} embeddings ({embeddings.First(e => !e.Exito).ErrorMensaje}). " +
                "El documento no se guardó; se reintentará en la próxima carga.");

        _log.LogInformation("{T}: {N} chunks | cultivo base: {C} | tema base: {Tm}",
            doc.Titulo, chunks.Count, cultivoDoc, temaDoc);

        // Todo o nada: un documento a medias nunca debe quedar activo.
        using (var tx = new TransactionScope(
                   TransactionScopeOption.Required,
                   new TransactionOptions
                   {
                       IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                       Timeout = TimeSpan.FromMinutes(5)
                   },
                   TransactionScopeAsyncFlowOption.Enabled))
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var c = chunks[i];
                var cultivo = _catalogo.DetectarCultivo(c.Contenido, c.Seccion) ?? cultivoDoc;
                var tema = _catalogo.DetectarTema(c.Contenido, c.Seccion) ?? temaDoc;

                var entidad = new EmbeddingChunks
                {
                    Titulo = TextoUtil.ParaVarchar(doc.Titulo, 255),
                    Contenido = c.Contenido,
                    Fuente = TextoUtil.ParaVarchar(Fuente, 200),
                    Cultivo = TextoUtil.ParaVarchar(cultivo, 100),
                    Tema = TextoUtil.ParaVarchar(tema, 150),
                    PalabrasClave = ConstruirPalabrasClave(c.Contenido, cultivo, tema),
                    FechaCarga = DateTime.UtcNow,
                    Activo = true,
                    VectorEmbedding = JsonSerializer.Serialize(embeddings[i].Vector),
                    Metadata = JsonSerializer.Serialize(new
                    {
                        version = VersionEsquema,
                        archivo,
                        archivoOriginal = doc.Titulo,
                        archivoHash = hash,
                        tipo = "manual_tecnico",
                        indice = c.Indice,
                        seccion = c.Seccion,
                        pagina = c.Pagina,
                        cultivo,
                        tema,
                        longitud = c.Contenido.Length,
                        total = chunks.Count          // permite detectar documentos incompletos con una consulta SQL
                    })
                };
                await _repo.CrearChunkAsync(entidad);
            }
            tx.Complete();
        }
        return true;
    }

    private static string ConstruirPalabrasClave(string texto, string cultivo, string tema)
    {
        var palabras = TextoUtil.PalabrasClave(texto, 25).ToList();
        if (cultivo != "General") palabras.Insert(0, cultivo.ToLowerInvariant());
        if (tema != "General") palabras.Insert(0, tema.ToLowerInvariant().Replace(' ', '_'));
        return TextoUtil.ParaVarchar(string.Join(",", palabras), 490, cortarEnComa: true);
    }

    private static async Task<string> HashAsync(string ruta, CancellationToken ct)
    {
        await using var fs = File.OpenRead(ruta);
        return Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
    }
}