using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>
/// MEJORAS: antes se usaba page.Text (orden del content stream, columnas mezcladas).
/// Ahora: palabras → bloques (Docstrum) → orden de lectura. Cada bloque ≈ un párrafo.
/// Si el análisis de layout falla en una página, cae a page.Text solo para esa página.
/// Verifica namespaces/nombres contra tu versión de PdfPig (0.1.9+).
/// </summary>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<PdfPigTextExtractor> _log;
    public PdfPigTextExtractor(ILogger<PdfPigTextExtractor> log) => _log = log;

    public DocumentoExtraido Extraer(string rutaPdf)
    {
        using var pdf = PdfDocument.Open(rutaPdf);
        var paginas = new List<PaginaExtraida>();
        foreach (var page in pdf.GetPages())
            paginas.Add(new PaginaExtraida(page.Number, ExtraerBloques(page)));

        return new DocumentoExtraido(Path.GetFileNameWithoutExtension(rutaPdf), paginas);
    }

    private IReadOnlyList<string> ExtraerBloques(Page page)
    {
        try
        {
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
            if (words.Count == 0) return Array.Empty<string>();

            var bloques = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var ordenados = new UnsupervisedReadingOrderDetector().Get(bloques);

            return ordenados.Select(b => b.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Layout analysis falló en página {Pagina}; se usa page.Text", page.Number);
            return string.IsNullOrWhiteSpace(page.Text) ? Array.Empty<string>() : new[] { page.Text };
        }
    }
}
