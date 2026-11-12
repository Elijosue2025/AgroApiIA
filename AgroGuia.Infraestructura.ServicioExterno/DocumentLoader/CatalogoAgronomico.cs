using System.Text.RegularExpressions;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Utilidades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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