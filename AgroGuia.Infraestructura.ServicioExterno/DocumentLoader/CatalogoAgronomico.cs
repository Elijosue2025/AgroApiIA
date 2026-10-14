using System.Text.RegularExpressions;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Utilidades;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>
/// MEJORAS sobre los diccionarios con string.Contains:
///  - Límites de palabra (\b): "papa" ya no coincide con "papaya" ni "biol" con "biología".
///  - Puntúa por número de coincidencias en vez de devolver el primer match del diccionario
///    (esa era la causa de que todo apareciera como Quinua / Buenas Prácticas Agrícolas).
///  - Se usa por CHUNK, con el documento solo como respaldo.
///  - Insensible a tildes (maíz = maiz).
/// </summary>
public sealed class CatalogoAgronomico : ICatalogoAgronomico
{
    private static Regex R(string p) => new(p, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly (string Nombre, Regex Patron)[] Cultivos =
    {
        ("Papa", R(@"\bpapas?\b")),
        ("Quinua", R(@"\b(quinua|quinoa)s?\b")),
        ("Maíz", R(@"\bmaiz\b")),
        ("Fréjol", R(@"\b(frejol|frijol)(es)?\b")),
        ("Hortalizas", R(@"\bhortalizas?\b")),
        ("Zanahoria", R(@"\bzanahorias?\b")),
        ("Cebolla", R(@"\bcebollas?\b")),
        ("Lechuga", R(@"\blechugas?\b")),
        ("Brócoli", R(@"\bbrocoli\b")),
        ("Tomate", R(@"\btomates?\b")),
        ("Arveja", R(@"\barvejas?\b")),
        ("Haba", R(@"\bhabas?\b")),
        ("Trigo", R(@"\btrigo\b")),
        ("Cebada", R(@"\bcebada\b")),
        ("Arroz", R(@"\barroz\b")),
    };

    private static readonly (string Nombre, Regex Patron)[] Temas =
    {
        ("Bioinsumos", R(@"\b(bioinsumos?|biol|bokashi|compost\w*|biofertiliz\w*|microorganismos eficientes)\b")),
        ("Fertilización", R(@"\b(abon\w*|fertiliz\w*|nutrici\w+|npk)\b")),
        ("Buenas Prácticas Agrícolas", R(@"\b(bpa|buenas practicas( agricolas)?)\b")),
        ("Sanidad Vegetal", R(@"\b(plagas?|enfermedad\w*|fungicidas?|insecticidas?|pesticidas?|tizon|gusano\w*|fitosanitari\w*)\b")),
        ("Riego y Agua", R(@"\b(riego\w*|irrigaci\w+)\b")),
        ("Cosecha y Postcosecha", R(@"\b(cosecha\w*|postcosecha|poscosecha|almacenamiento)\b")),
        ("Manejo de Suelos", R(@"\b(suelos?|erosion|labranza)\b")),
        ("Siembra y Trasplante", R(@"\b(siembra\w*|sembrar|trasplante\w*)\b")),
        ("Semillas", R(@"\b(semillas?|germinaci\w+)\b")),
        ("Clima y Altitud", R(@"\b(clima\w*|altitud|heladas?|temperatura\w*|precipitaci\w+)\b")),
    };

    public string? DetectarCultivo(string texto, string? titulo = null) => Mejor(Cultivos, texto, titulo);
    public string? DetectarTema(string texto, string? titulo = null) => Mejor(Temas, texto, titulo);

    private static string? Mejor((string Nombre, Regex Patron)[] catalogo, string texto, string? titulo)
    {
        var t = TextoUtil.ParaBusqueda(texto ?? string.Empty);
        var ti = string.IsNullOrWhiteSpace(titulo) ? string.Empty : TextoUtil.ParaBusqueda(titulo);
        string? mejor = null;
        int mejorPuntaje = 0;
        foreach (var (nombre, patron) in catalogo)
        {
            int p = patron.Matches(t).Count + 5 * (ti.Length == 0 ? 0 : patron.Matches(ti).Count);
            if (p > mejorPuntaje) { mejorPuntaje = p; mejor = nombre; }
        }
        return mejor;
    }
}
