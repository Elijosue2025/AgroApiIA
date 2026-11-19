using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.Utilidades;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Transactions;
using UglyToad.PdfPig.Content;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>
/// Lee CatalogoTerminos de BD y lo cachea en memoria.
/// Singleton: usa IServiceScopeFactory porque el repo es scoped.
/// </summary>
public sealed class CatalogoAgronomico : ICatalogoAgronomico
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CatalogoAgronomico> _log;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private volatile List<(string Nombre, string Tipo, Regex Patron)>? _terminos;

    public CatalogoAgronomico(IServiceScopeFactory scopes, ILogger<CatalogoAgronomico> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public string? DetectarCultivo(string texto, string? titulo = null)
        => Detectar(texto, titulo, "Cultivo");

    public string? DetectarTema(string texto, string? titulo = null)
        => Detectar(texto, titulo, "Tema")
        ?? Detectar(texto, titulo, "Plaga")
        ?? Detectar(texto, titulo, "Enfermedad")
        ?? Detectar(texto, titulo, "Mantenimiento");

    public string? DetectarPlagaOEnfermedad(string texto, string? titulo = null)
        => Detectar(texto, titulo, "Plaga")
        ?? Detectar(texto, titulo, "Enfermedad");

    /// <summary>Invalida la caché (llamar después de agregar términos nuevos).</summary>
    public void Invalidar() => _terminos = null;

    private string? Detectar(string texto, string? titulo, string tipo)
    {
        var lista = ObtenerTerminos();
        var candidatos = lista.Where(t => t.Tipo.Equals(tipo, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidatos.Count == 0) return null;

        var t = TextoUtil.ParaBusqueda(texto ?? string.Empty);
        var ti = string.IsNullOrWhiteSpace(titulo) ? string.Empty : TextoUtil.ParaBusqueda(titulo);

        string? mejor = null;
        int mejorPuntaje = 0;

        foreach (var (nombre, _, patron) in candidatos)
        {
            int p = patron.Matches(t).Count + 5 * (ti.Length == 0 ? 0 : patron.Matches(ti).Count);
            if (p > mejorPuntaje)
            {
                mejorPuntaje = p;
                mejor = nombre;
            }
        }

        return mejor;
    }
    public IReadOnlyList<string> DetectarEnfermedades(string texto)
    {
        var t = TextoUtil.ParaBusqueda(texto ?? "");
        return ObtenerTerminos()
            .Where(x => x.Tipo.Equals("Enfermedad", StringComparison.OrdinalIgnoreCase) && x.Patron.IsMatch(t))
            .Select(x => x.Nombre).Distinct().ToList();
    }

    public bool MencionaTizonGenerico(string texto) =>
        ObtenerTerminos().Any(x => x.Tipo == "EnfermedadGenerica" && x.Patron.IsMatch(TextoUtil.ParaBusqueda(texto ?? "")));

    // "amistar top" gana sobre "amistar": se elige el nombre más largo
    public string? DetectarProducto(string texto)
    {
        var t = TextoUtil.ParaBusqueda(texto ?? "");
        return ObtenerTerminos()
            .Where(x => x.Tipo == "Producto" && x.Patron.IsMatch(t))
            .OrderByDescending(x => x.Nombre.Length).Select(x => x.Nombre).FirstOrDefault();
    }
    public sealed record MetaDoc(string Titulo, string Cultivo, string[] Enfermedades, string Tipo,
    string? Producto, string Confianza, string Fuente, string[] Claves);
    public sealed record SeccionMd(string Titulo, string Texto);
    public sealed record DocumentoMd(MetaDoc Meta, IReadOnlyList<SeccionMd> Secciones);

    public static class MarkdownDocumentParser
    {
        private static readonly string[] Tipos = { "manual_tecnico", "ficha_comercial", "comparativo", "fertilizante" };

        public static DocumentoMd Parsear(string md, string archivo)
        {
            var m = Regex.Match(md, @"\A---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline);
            if (!m.Success) throw new InvalidDataException($"{archivo}: falta el bloque --- de metadatos.");

            var kv = new Dictionary<string, string>();
            foreach (var l in m.Groups[1].Value.Split('\n'))
            {
                var p = l.Split(':', 2);
                if (p.Length == 2) kv[p[0].Trim().ToLowerInvariant()] = Regex.Replace(p[1], @"\s+#.*$", "").Trim();
            }
            string Req(string k) => kv.TryGetValue(k, out var v) && v.Length > 0
                ? v : throw new InvalidDataException($"{archivo}: falta '{k}' en los metadatos.");

            var tipo = Req("tipo");
            if (!Tipos.Contains(tipo)) throw new InvalidDataException($"{archivo}: tipo '{tipo}' no válido.");
            var enf = kv.GetValueOrDefault("enfermedad", "")
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tipo is "manual_tecnico" or "comparativo" && enf.Length == 0)
                throw new InvalidDataException($"{archivo}: este tipo requiere 'enfermedad'.");

            var meta = new MetaDoc(Req("titulo"), Req("cultivo"), enf, tipo, kv.GetValueOrDefault("producto"),
                kv.GetValueOrDefault("confianza", "cientifica"), Req("fuente"),
                kv.GetValueOrDefault("palabras_clave", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            var secciones = new List<SeccionMd>();
            foreach (var bloque in Regex.Split(md[m.Length..], @"^##\s+", RegexOptions.Multiline))
            {
                if (string.IsNullOrWhiteSpace(bloque)) continue;
                var i = bloque.IndexOf('\n');
                secciones.Add(i < 0 ? new SeccionMd("Introducción", bloque.Trim())
                                    : new SeccionMd(bloque[..i].Trim(), bloque[(i + 1)..].Trim()));
            }
            return new DocumentoMd(meta, secciones);
        }
    }
    public static class SectionChunker
    {
        public static IEnumerable<ChunkPreparado> Dividir(DocumentoMd doc, int max)
        {
            int idx = 0;
            foreach (var s in doc.Secciones)
            {
                var sb = new StringBuilder();
                foreach (var p in Regex.Split(s.Texto, @"\n\s*\n").Where(x => x.Trim().Length > 0).SelectMany(x => Trocear(x.Trim(), max)))
                {
                    if (sb.Length > 0 && sb.Length + p.Length + 2 > max)
                    { yield return new ChunkPreparado(idx++, sb.ToString(), s.Titulo, 0); sb.Clear(); }
                    if (sb.Length > 0) sb.Append("\n\n");
                    sb.Append(p);
                }
                if (sb.Length > 0) yield return new ChunkPreparado(idx++, sb.ToString(), s.Titulo, 0);
            }
        }

        private static IEnumerable<string> Trocear(string p, int max)
        {
            if (p.Length <= max) { yield return p; yield break; }
            var sb = new StringBuilder();
            foreach (var o in Regex.Split(p, @"(?<=[.!?;])\s+"))
            {
                if (sb.Length > 0 && sb.Length + o.Length + 1 > max) { yield return sb.ToString(); sb.Clear(); }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(o);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }
    }

   
    private List<(string Nombre, string Tipo, Regex Patron)> ObtenerTerminos()
    {
        var cached = _terminos;
        if (cached is not null) return cached;

        _lock.Wait();
        try
        {
            if (_terminos is not null) return _terminos;

            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICatalogoTerminosRepositorio>();
            var entidades = repo.ObtenerActivosAsync().GetAwaiter().GetResult();

            var lista = new List<(string, string, Regex)>();
            foreach (var e in entidades)
            {
                var partes = new List<string> { e.Nombre };
                if (!string.IsNullOrWhiteSpace(e.Sinonimos))
                {
                    partes.AddRange(e.Sinonimos
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }

                // Escapar y unir con | para \b(termino1|termino2)\b
                var pattern = @"\b(" + string.Join("|", partes
                    .Select(p => Regex.Escape(TextoUtil.ParaBusqueda(p)))
                    .Where(p => p.Length > 1)) + @")\b";

                if (pattern == @"\b()\b") continue;

                lista.Add((e.Nombre, e.Tipo, new Regex(pattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)));
            }

            _log.LogInformation("Catálogo agronómico cargado: {N} términos activos", lista.Count);
            _terminos = lista;
            return lista;
        }
        finally
        {
            _lock.Release();
        }
    }
}