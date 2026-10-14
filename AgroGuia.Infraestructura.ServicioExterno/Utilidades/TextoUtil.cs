using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AgroGuia.Infraestructura.ServicioExterno.Utilidades;

/// <summary>
/// Utilidades compartidas por ingesta (palabras clave) y búsqueda (tokens BM25),
/// de modo que ambos lados normalicen EXACTAMENTE igual.
/// </summary>
public static class TextoUtil
{
    private static readonly Regex Palabra = new(@"[\p{L}\p{Nd}]+", RegexOptions.Compiled);
    private static readonly Regex PalabraLarga = new(@"\p{L}{5,}", RegexOptions.Compiled);

    // Sin tildes y en minúscula (se compara contra texto ya normalizado).
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "para","como","este","esta","estos","estas","desde","hasta","entre","sobre","todos","todas",
        "puede","pueden","tiene","tienen","hacer","debe","deben","cada","tambien","cuando","donde",
        "cuanto","cuantos","cuanta","segun","dicha","dicho","mientras","ademas","traves","durante",
        "mediante","tanto","siendo","otros","otras","mismo","misma","pero","porque","cual","cuales",
        "que","quien","muy","mas","menos","sus","con","sin","los","las","del","por","una","uno","unos",
        "unas","ser","son","fue","han","hay","esto","eso","ese","esa","aqui","asi","solo","hacia","ante",
        "bajo","tras","les","nos","sea","sean","quiero","necesito","dime","puedo","debo","recomienda",
        "recomiendas","favor","hola","buenas","gracias"
    };

    public static string QuitarTildes(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string ParaBusqueda(string s) => QuitarTildes(s).ToLowerInvariant();

    public static bool EsStopWord(string tokenSinTildes) => StopWords.Contains(tokenSinTildes);

    /// <summary>Raíz ligera: papas→papa, enfermedades→enfermedad, frejoles→frejol.</summary>
    public static string Raiz(string t)
    {
        if (t.Length > 5 && t.EndsWith("es", StringComparison.Ordinal) && "dlnrz".IndexOf(t[^3]) >= 0)
            return t[..^2];
        if (t.Length > 4 && t[^1] == 's') return t[..^1];
        return t;
    }

    public static IEnumerable<string> Tokenizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) yield break;
        foreach (Match m in Palabra.Matches(ParaBusqueda(texto)))
        {
            var t = m.Value;
            if (t.Length < 3 || StopWords.Contains(t) || t.All(char.IsDigit)) continue;
            yield return Raiz(t);
        }
    }

    /// <summary>Palabras clave legibles (con tildes), por frecuencia y no por "primeras 28 distintas".</summary>
    public static IReadOnlyList<string> PalabrasClave(string texto, int max)
    {
        var conteo = new Dictionary<string, (int N, int Orden)>();
        int orden = 0;
        foreach (Match m in PalabraLarga.Matches(texto.ToLowerInvariant()))
        {
            var w = m.Value;
            if (StopWords.Contains(QuitarTildes(w))) continue;
            conteo[w] = conteo.TryGetValue(w, out var v) ? (v.N + 1, v.Orden) : (1, orden++);
        }
        return conteo.OrderByDescending(kv => kv.Value.N).ThenBy(kv => kv.Value.Orden)
                     .Take(max).Select(kv => kv.Key).ToList();
    }

    /// <summary>
    /// Las columnas Titulo/Fuente/Cultivo/Tema/PalabrasClave son VARCHAR (no NVARCHAR):
    /// todo lo que no entra en Latin-1 se convertiría en '?'. Se sanea y se trunca al tamaño real.
    /// </summary>
    public static string ParaVarchar(string? s, int max, bool cortarEnComa = false)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormC))
        {
            switch (c)
            {
                case '\u2013': case '\u2014': case '\u2022': sb.Append('-'); break;
                case '\u201C': case '\u201D': sb.Append('"'); break;
                case '\u2018': case '\u2019': sb.Append('\''); break;
                case '\u2026': sb.Append("..."); break;
                case '\u00A0': sb.Append(' '); break;
                default: if (c <= 0xFF && !char.IsControl(c)) sb.Append(c); break;
            }
        }
        var r = sb.ToString().Trim();
        if (r.Length <= max) return r;
        r = r[..max];
        if (cortarEnComa)
        {
            int i = r.LastIndexOf(',');
            if (i > 0) r = r[..i];
        }
        return r.TrimEnd();
    }
}
