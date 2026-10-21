using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.RAG;
using AgroGuia.Infraestructura.ServicioExterno.Vision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class DiagnosticoServicioImpl : IDiagnosticoServicio
{
    private const string Advertencia =
        "Resultado orientativo generado por visión artificial. Confírmalo con un técnico agropecuario " +
        "antes de aplicar productos fitosanitarios.";

    private const int MaxCaracteresRecomendacion = 700;

    private readonly IAnalizadorVision _vision;
    private readonly ContextRetriever _contextRetriever;
    private readonly VisionOptions _opt;
    private readonly ILogger<DiagnosticoServicioImpl> _log;

    public DiagnosticoServicioImpl(
        IAnalizadorVision vision,
        ContextRetriever contextRetriever,
        IOptions<VisionOptions> opt,
        ILogger<DiagnosticoServicioImpl> log)
    {
        _vision = vision;
        _contextRetriever = contextRetriever;
        _opt = opt.Value;
        _log = log;
    }

    public DiagnosticoEstadoDto ObtenerEstado()
    {
        var e = _vision.ObtenerEstado();
        return new DiagnosticoEstadoDto
        {
            ModeloDisponible = e.ModeloDisponible,
            Modelo = e.Modelo,
            NumeroEtiquetas = e.NumeroEtiquetas,
            Detalle = e.Detalle
        };
    }

    public async Task<DiagnosticoImagenResponseDto> AnalizarImagenAsync(
        byte[] imagen, string? cultivo = null, CancellationToken ct = default)
    {
        var r = await _vision.AnalizarAsync(imagen, ct);

        if (!r.Exito)
        {
            return new DiagnosticoImagenResponseDto
            {
                Exito = false,
                ErrorMensaje = r.ErrorMensaje,
                Advertencia = Advertencia,
                FechaRespuesta = DateTime.UtcNow
            };
        }

        var dto = new DiagnosticoImagenResponseDto
        {
            Exito = true,
            ModeloDisponible = r.ModeloDisponible,
            Detecciones = r.Detecciones.Select(d => Mapear(d)).ToList(),
            Lesiones = r.Lesiones is null ? null : Mapear(r.Lesiones),
            Advertencia = Advertencia,
            TiempoMs = r.TiempoMs,
            FechaRespuesta = DateTime.UtcNow
        };

        var principal = r.Detecciones.FirstOrDefault();

        if (_opt.IncluirRecomendacionesRag && principal is not null
            && principal.Etiqueta.Tipo is TipoHallazgo.Enfermedad or TipoHallazgo.Plaga)
        {
            dto.Recomendaciones = await BuscarRecomendacionesAsync(principal.Etiqueta, cultivo, ct);
        }

        dto.Resumen = ConstruirResumen(r, principal);
        return dto;
    }

    // ------------------------------------------------------------ RAG

    /// <summary>
    /// Busca en los manuales cómo manejar lo detectado. Es un complemento: si Ollama o el índice fallan,
    /// el diagnóstico se entrega igual, sin recomendaciones.
    /// </summary>
    private async Task<List<RecomendacionDto>> BuscarRecomendacionesAsync(
        EtiquetaVision etiqueta, string? cultivoUsuario, CancellationToken ct)
    {
        try
        {
            var cultivo = etiqueta.Cultivo ?? cultivoUsuario;
            var pregunta = string.IsNullOrWhiteSpace(cultivo)
                ? $"¿Cómo se identifica y se maneja o controla {etiqueta.Nombre}?"
                : $"¿Cómo se identifica y se maneja o controla {etiqueta.Nombre} en {cultivo}?";

            var contextos = await _contextRetriever.ObtenerContextoAsync(pregunta, ct);

            return contextos
                .Take(Math.Max(1, _opt.MaxRecomendaciones))
                .Select(c => new RecomendacionDto
                {
                    Titulo = c.Titulo,
                    Seccion = c.Seccion,
                    Pagina = c.Pagina,
                    Texto = Recortar(c.Texto, MaxCaracteresRecomendacion)
                })
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron obtener recomendaciones de los manuales para '{Etiqueta}'", etiqueta.Nombre);
            return new List<RecomendacionDto>();
        }
    }

    // ------------------------------------------------------------ mapeo y texto

    private static DeteccionDto Mapear(DeteccionVision d) => new()
    {
        Clave = d.Etiqueta.Clave,
        Nombre = d.Etiqueta.Nombre,
        Cultivo = d.Etiqueta.Cultivo,
        Tipo = d.Etiqueta.Tipo.ToString(),
        Confianza = Math.Round(d.Confianza, 4),
        Descripcion = d.Etiqueta.Descripcion
    };

    private static LesionesDto Mapear(ResultadoLesiones l) => new()
    {
        TejidoDetectado = l.TejidoVegetalDetectado,
        PorcentajeAfectado = l.PorcentajeAfectado,
        NumeroManchas = l.NumeroManchas,
        Severidad = l.Severidad
    };

    private static string ConstruirResumen(ResultadoVision r, DeteccionVision? principal)
    {
        var partes = new List<string>();

        if (!r.ModeloDisponible)
        {
            partes.Add("El modelo de reconocimiento aún no está configurado; solo se analizó el color de la hoja.");
        }
        else if (principal is null)
        {
            partes.Add("No se pudo identificar la plaga o enfermedad con suficiente confianza. " +
                       "Prueba con una foto más cercana, con buena luz y la hoja bien enfocada.");
        }
        else
        {
            var pct = $"{principal.Confianza * 100:0}%";
            var cultivo = string.IsNullOrWhiteSpace(principal.Etiqueta.Cultivo)
                ? string.Empty
                : $" en {principal.Etiqueta.Cultivo}";

            partes.Add(principal.Etiqueta.Tipo switch
            {
                TipoHallazgo.Sano => $"La planta parece sana ({pct} de confianza).",
                TipoHallazgo.Plaga => $"Posible plaga: {principal.Etiqueta.Nombre}{cultivo} ({pct} de confianza).",
                TipoHallazgo.Enfermedad => $"Posible enfermedad: {principal.Etiqueta.Nombre}{cultivo} ({pct} de confianza).",
                _ => $"Posible coincidencia: {principal.Etiqueta.Nombre}{cultivo} ({pct} de confianza)."
            });
        }

        if (r.Lesiones is { TejidoVegetalDetectado: true } l)
        {
            partes.Add($"Aproximadamente {l.PorcentajeAfectado:0.#}% del tejido visible muestra daño por color " +
                       $"(severidad {l.Severidad.ToLowerInvariant()}).");
        }

        return string.Join(" ", partes);
    }

    private static string Recortar(string texto, int max)
    {
        texto = texto.Trim();
        return texto.Length <= max ? texto : texto[..max].TrimEnd() + "…";
    }
}