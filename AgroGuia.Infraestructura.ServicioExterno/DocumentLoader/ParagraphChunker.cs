using System.Text;
using System.Text.RegularExpressions;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>
/// MEJORAS sobre DividirEnChunksInteligente (Substring por caracteres):
///  - Nunca corta a mitad de palabra. En tu tabla se ven chunks que empiezan con
///    "orado a partir de...", "CIÓN GENERAL", "osicionándose" y palabras clave como "rganizaciones":
///    era el efecto de `posicion = fin - overlap`.
///  - Nunca pierde texto (antes, cortar en el último punto y avanzar desde `fin` saltaba cientos de caracteres).
///  - Agrupa por párrafos/bloques, respeta secciones (detecta títulos) y guarda página y sección.
///  - Parte párrafos gigantes por oraciones sin cortar "Ing.", "Fig." ni "2.5 kg".
///  - Descarta chunks con poca proporción de letras (tablas rotas, índices).
/// AJUSTES (verificados con pruebas):
///  - Una lista numerada ("1. Llenar el tanque", "2. Agregar el estiércol") se tomaba por TÍTULOS de sección:
///    cada paso reemplazaba al anterior y el texto desaparecía del contenido Y de la sección. Ahora la numeración
///    solo cuenta como título si tiene subniveles ("2.1 Plagas"); "1. INTRODUCCIÓN" sigue siendo título por ir en mayúsculas.
///  - Las líneas de pura basura ("· ~ ^ 1 ; 2 |") se descartan por separado: antes sobrevivían dentro de un chunk bueno.
///  - Registra cuánto descarta, para poder calibrar los umbrales con tus PDFs.
/// </summary>
public sealed class ParagraphChunker : IChunker
{
    // Solo numeración con subniveles (2.1, 3.2.1). Un "1." o "1)" suelto es un paso de lista, no un título.
    private static readonly Regex Numerado = new(@"^\d{1,2}(\.\d{1,2}){1,3}[.)]?\s+\p{Lu}", RegexOptions.Compiled);
    private static readonly Regex PalabraTitulo = new(@"^(CAP[IÍ]TULO|ANEXO|M[OÓ]DULO|SECCI[OÓ]N|UNIDAD|TEMA)\b", RegexOptions.Compiled);
    private static readonly Regex FinOracion = new(@"(?<=[.!?;])\s+(?=[\p{Lu}¿¡])", RegexOptions.Compiled);
    private static readonly Regex Abreviatura = new(
        @"(?:\b(?:Ing|Dr|Dra|Sr|Sra|Lic|Fig|Cap|Vol|Núm|Prof|Msc|Mtr)|\b\p{Lu})\.$", RegexOptions.Compiled);

    private sealed record Unidad(string Texto, int Pagina, string? Seccion);
    private readonly ChunkingOptions _o;
    private readonly ILogger<ParagraphChunker> _log;

    public ParagraphChunker(IOptions<ChunkingOptions> opt, ILogger<ParagraphChunker> log)
    {
        _o = opt.Value;
        _log = log;
    }

    public IReadOnlyList<ChunkPreparado> Dividir(IReadOnlyList<BloqueTexto> bloques)
    {
        // 1) Bloques → unidades (títulos solo actualizan la sección actual)
        var unidades = new List<Unidad>();
        string? seccion = null;
        int sinLetras = 0, pocoTexto = 0, muyCortos = 0;
        var muestras = new List<string>();

        foreach (var b in bloques)
        {
            if (EsTitulo(b.Texto)) { seccion = b.Texto; continue; }
            foreach (var parte in PartirSiEsLargo(b.Texto))
            {
                if (RatioLetras(parte) < _o.RatioLetrasUnidadMinimo) { sinLetras++; continue; }
                unidades.Add(new Unidad(parte, b.Pagina, seccion));
            }
        }

        // 2) Agrupar unidades hasta MaxChars sin cruzar secciones
        var crudos = new List<(string Texto, string? Seccion, int Pagina)>();
        var actual = new List<Unidad>();
        int len = 0, nuevos = 0;

        foreach (var u in unidades)
        {
            bool cambioSeccion = actual.Count > 0 && actual[^1].Seccion != u.Seccion;
            if (nuevos > 0 && (cambioSeccion || len + u.Texto.Length + 2 > _o.MaxChars))
            {
                crudos.Add(Armar(actual));
                actual = cambioSeccion ? new List<Unidad>() : actual.TakeLast(_o.OverlapParrafos).ToList();
                len = actual.Sum(x => x.Texto.Length + 2);
                nuevos = 0;
            }
            actual.Add(u); len += u.Texto.Length + 2; nuevos++;
        }
        if (nuevos > 0) crudos.Add(Armar(actual));

        // 3) Filtrado de calidad y fusión de restos pequeños
        var finales = new List<(string Texto, string? Seccion, int Pagina)>();
        foreach (var c in crudos)
        {
            if (RatioLetras(c.Texto) < _o.RatioLetrasMinimo)
            {
                pocoTexto++;
                if (muestras.Count < 3) muestras.Add(c.Texto.Length > 80 ? c.Texto[..80] : c.Texto);
                continue;
            }

            if (c.Texto.Length < _o.MinChars)
            {
                if (finales.Count > 0 && finales[^1].Seccion == c.Seccion &&
                    finales[^1].Texto.Length + c.Texto.Length + 2 <= _o.MaxChars * 1.3)
                {
                    var p = finales[^1];
                    finales[^1] = (p.Texto + "\n\n" + c.Texto, p.Seccion, p.Pagina);
                    continue;
                }
                if (c.Texto.Length < 60) { muyCortos++; continue; }
            }
            finales.Add(c);
        }

        if (sinLetras + pocoTexto + muyCortos > 0)
            _log.LogInformation(
                "Chunking: {N} chunks | descartado: {A} líneas sin letras, {B} chunks con poco texto, {C} chunks muy cortos. " +
                "Ejemplos de poco texto: {M}",
                finales.Count, sinLetras, pocoTexto, muyCortos, string.Join(" || ", muestras));

        return finales.Select((c, i) => new ChunkPreparado(i, c.Texto, c.Seccion, c.Pagina)).ToList();
    }

    private static (string, string?, int) Armar(List<Unidad> u) =>
        (string.Join("\n\n", u.Select(x => x.Texto)), u[0].Seccion, u[0].Pagina);

    private static double RatioLetras(string s)
    {
        int visibles = 0, letras = 0;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c)) continue;
            visibles++;
            if (char.IsLetter(c)) letras++;
        }
        return visibles == 0 ? 0 : (double)letras / visibles;
    }

    private static bool EsTitulo(string t)
    {
        if (t.Length < 3 || t.Length > 100) return false;
        if (t[^1] is '.' or ',' or ';' or ':') return false;
        int palabras = t.Count(c => c == ' ') + 1;

        if (PalabraTitulo.IsMatch(t)) return true;
        if (Numerado.IsMatch(t)) return palabras <= 10;

        int letras = t.Count(char.IsLetter);
        if (letras < 4) return false;
        int mayus = t.Count(char.IsUpper);
        return palabras <= 12 && (double)mayus / letras >= 0.8;
    }

    private IEnumerable<string> PartirSiEsLargo(string texto)
    {
        if (texto.Length <= _o.MaxChars) { yield return texto; yield break; }

        var oraciones = new List<string>();
        foreach (var o in FinOracion.Split(texto))
        {
            if (oraciones.Count > 0 && Abreviatura.IsMatch(oraciones[^1])) oraciones[^1] += " " + o;
            else oraciones.Add(o);
        }

        var sb = new StringBuilder();
        foreach (var o in oraciones)
            foreach (var trozo in CortarPorPalabras(o))
            {
                if (sb.Length > 0 && sb.Length + trozo.Length + 1 > _o.MaxChars)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(trozo);
            }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private IEnumerable<string> CortarPorPalabras(string s)
    {
        if (s.Length <= _o.MaxChars) { yield return s; yield break; }
        int pos = 0;
        while (pos < s.Length)
        {
            int fin = Math.Min(pos + _o.MaxChars, s.Length);
            if (fin < s.Length)
            {
                int sp = s.LastIndexOf(' ', fin - 1, fin - pos);
                if (sp > pos) fin = sp;
            }
            yield return s.Substring(pos, fin - pos).Trim();
            pos = fin;
            while (pos < s.Length && s[pos] == ' ') pos++;
        }
    }
}
