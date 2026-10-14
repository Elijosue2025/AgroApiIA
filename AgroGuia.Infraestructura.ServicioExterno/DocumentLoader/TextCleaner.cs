using System.Text;
using System.Text.RegularExpressions;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>
/// MEJORAS: la versión anterior hacía Regex.Replace(@"\s+", " ") sobre TODO el documento,
/// eliminando cualquier estructura. Ahora se limpia por bloque y se conservan los párrafos.
///  - Normalización Unicode FormKC (ligaduras "ﬁ", superíndices).
///  - Une palabras cortadas por guion de fin de línea (fertili- zación).
///  - Elimina encabezados/pies repetidos (>= 40% de las páginas) y números de página.
///  - Elimina líneas de índice ("Capítulo 1 ........ 12").
/// </summary>
public sealed class TextCleaner : ITextCleaner
{
    private static readonly Regex Hyphen = new(@"(?<=\p{L}{2})-\s+(?=\p{Ll})", RegexOptions.Compiled);
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Puntos = new(@"\.{3,}|(\.\s){4,}", RegexOptions.Compiled);
    private static readonly Regex LineaIndice = new(@"(\.{4,}|(\.\s){4,})\s*\d+\s*$", RegexOptions.Compiled);
    private static readonly Regex SoloNumero = new(@"^\W*\d{1,4}\W*$", RegexOptions.Compiled);
    private static readonly Regex Digitos = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex Control = new(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", RegexOptions.Compiled);

    public IReadOnlyList<BloqueTexto> Limpiar(DocumentoExtraido documento)
    {
        int totalPaginas = documento.Paginas.Count;

        // Firma de bloques cortos → en cuántas páginas distintas aparece.
        var paginasPorFirma = new Dictionary<string, HashSet<int>>();
        foreach (var p in documento.Paginas)
            foreach (var b in p.Bloques.Where(b => b.Length <= 150))
                (paginasPorFirma.TryGetValue(Firma(b), out var set)
                    ? set : paginasPorFirma[Firma(b)] = new HashSet<int>()).Add(p.Numero);

        var ruido = totalPaginas >= 4
            ? paginasPorFirma.Where(kv => (double)kv.Value.Count / totalPaginas >= 0.40)
                             .Select(kv => kv.Key).ToHashSet()
            : new HashSet<string>();

        var resultado = new List<BloqueTexto>();
        foreach (var p in documento.Paginas)
            foreach (var crudo in p.Bloques)
            {
                if (LineaIndice.IsMatch(crudo)) continue;
                if (crudo.Length <= 150 && ruido.Contains(Firma(crudo))) continue;

                var t = Normalizar(crudo);
                if (t.Length == 0 || SoloNumero.IsMatch(t)) continue;
                resultado.Add(new BloqueTexto(p.Numero, t));
            }
        return resultado;
    }

    private static string Firma(string s) =>
        Digitos.Replace(Espacios.Replace(s.Trim().ToLowerInvariant(), " "), "#");

    private static string Normalizar(string s)
    {
        s = s.Normalize(NormalizationForm.FormKC).Replace("\u00AD", "");
        s = Control.Replace(s, " ");
        s = Hyphen.Replace(s, "");
        s = s.Replace('–', '-').Replace('—', '-');
        s = Puntos.Replace(s, " ");
        return Espacios.Replace(s, " ").Trim();
    }
}
