using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.RAG;
using AgroGuia.Infraestructura.ServicioExterno.Vision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class DiagnosticoServicioImpl : IDiagnosticoServicio
{
    private const string AdvertenciaBase =
        "Resultado orientativo por visión artificial. No sustituye diagnóstico de laboratorio ni de un técnico agrónomo. " +
        "Confirma antes de aplicar cualquier producto fitosanitario.";

    private const string SistemaVisionRag = """
Eres AgroGuía IA, asistente agronómico. El usuario envió una FOTO de un cultivo y un análisis visual automático.
REGLAS:
1. Usa SOLO el CONTEXTO de manuales y el RESULTADO DE VISIÓN. No inventes plagas, dosis ni marcas.
2. Si la confianza de visión es baja o el estado es Incierta/SinDeteccion/SinModelo, dilo con claridad: no afirmes un diagnóstico definitivo; pide mejor foto o revisión técnica.
3. Estructura cuando haya información suficiente:
   - Qué sugiere el análisis de imagen (con confianza)
   - Síntomas / causas solo si están en el contexto
   - Manejo: monitoreo, cultural, biológico; productos químicos solo si aparecen en el contexto
4. Cita fragmentos con [1], [2] según el orden del CONTEXTO.
5. Español claro, sin relleno ni "excelente pregunta".
6. Si el contexto no cubre el manejo, dilo en una frase.
""";

    private const int MaxCaracteresRecomendacion = 700;

    private readonly IAnalizadorVision _vision;
    private readonly ContextRetriever _contextRetriever;
    private readonly IChatCompletionService _chat;
    private readonly VisionOptions _opt;
    private readonly ILogger<DiagnosticoServicioImpl> _log;
    private readonly IVisionIncidenciaRepositorio _incidencias;

    public DiagnosticoServicioImpl(
        IAnalizadorVision vision,
        ContextRetriever contextRetriever,
        IChatCompletionService chat,
        IOptions<VisionOptions> opt,
        ILogger<DiagnosticoServicioImpl> log,
        IVisionIncidenciaRepositorio incidencias)
    {
        _vision = vision;
        _contextRetriever = contextRetriever;
        _chat = chat;
        _opt = opt.Value;
        _log = log;
        _incidencias = incidencias;
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
        byte[] imagen,
        string? cultivo = null,
        long usuarioId = 0,
        long? conversacionId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var cultivoNorm = NormalizarCultivo(cultivo) ?? "papa";

        var r = await _vision.AnalizarAsync(imagen, ct);
        if (!r.Exito)
        {
            var errorDto = new DiagnosticoImagenResponseDto
            {
                Exito = false,
                ErrorMensaje = r.ErrorMensaje,
                Advertencia = AdvertenciaBase,
                FechaRespuesta = DateTime.UtcNow,
                TiempoMs = sw.ElapsedMilliseconds,
                Estado = "Error",
                Cultivo = cultivoNorm
            };

            try
            {
                await GuardarIncidenciaAsync(usuarioId, conversacionId, errorDto, null, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo guardar incidencia de error de visión");
            }

            return errorDto;
        }

        var principal = r.Detecciones.FirstOrDefault();
        var confianza = principal?.Confianza;
        var estado = ResolverEstado(r, principal, confianza);

        var dto = new DiagnosticoImagenResponseDto
        {
            Exito = true,
            ModeloDisponible = r.ModeloDisponible,
            Detecciones = r.Detecciones.Select(Mapear).ToList(),
            Lesiones = r.Lesiones is null ? null : Mapear(r.Lesiones),
            Advertencia = AdvertenciaBase,
            TiempoMs = r.TiempoMs,
            FechaRespuesta = DateTime.UtcNow,
            Estado = estado,
            ConfianzaPrincipal = confianza,
            ClasePredicha = principal?.Etiqueta.Nombre,
            Cultivo = principal?.Etiqueta.Cultivo ?? cultivoNorm,
            Resumen = ConstruirResumenVision(r, principal, estado, confianza)
        };

        var preguntaRag = ConstruirPreguntaRag(principal, cultivoNorm, estado);
        IReadOnlyList<ContextoRecuperado> contextos = Array.Empty<ContextoRecuperado>();

        try
        {
            if (_opt.IncluirRecomendacionesRag)
                contextos = await _contextRetriever.ObtenerContextoAsync(preguntaRag, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "RAG falló en diagnóstico; se continúa sin fragmentos");
        }

        dto.Recomendaciones = contextos
            .Take(Math.Max(1, _opt.MaxRecomendaciones))
            .Select(c => new RecomendacionDto
            {
                Titulo = c.Titulo,
                Seccion = c.Seccion,
                Pagina = c.Pagina,
                Texto = Recortar(c.Texto, MaxCaracteresRecomendacion)
            })
            .ToList();

        dto.Fuentes = contextos
            .Select((c, i) =>
            {
                var p = c.Pagina is null ? "" : $", pág. {c.Pagina}";
                var sec = string.IsNullOrWhiteSpace(c.Seccion) ? "" : $" — {c.Seccion}";
                return $"[{i + 1}] {c.Titulo}{sec}{p}";
            })
            .ToList();

        dto.RespuestaAsistente = await GenerarRespuestaAsistenteAsync(
            dto.Resumen, estado, confianza, cultivoNorm, principal, contextos, ct);

        try
        {
            await GuardarIncidenciaAsync(usuarioId, conversacionId, dto, null, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo guardar la incidencia de visión");
        }

        dto.TiempoMs = sw.ElapsedMilliseconds;
        return dto;
    }

    public async Task<IReadOnlyList<VisionIncidenciaDto>> ListarIncidenciasMiasAsync(
        long usuarioId, int take = 50, CancellationToken ct = default)
    {
        var list = await _incidencias.ListarPorUsuarioAsync(usuarioId, take, ct);
        return list.Select(MapIncidencia).ToList();
    }

    public async Task<VisionIncidenciaDto?> ObtenerIncidenciaMiaAsync(
        long usuarioId, long id, CancellationToken ct = default)
    {
        var x = await _incidencias.ObtenerPorIdAsync(id, ct);
        if (x is null || x.UsuarioId != usuarioId)
            return null;
        return MapIncidencia(x);
    }

    private async Task GuardarIncidenciaAsync(
        long usuarioId,
        long? conversacionId,
        DiagnosticoImagenResponseDto dto,
        string? modeloVersion,
        CancellationToken ct)
    {
        if (usuarioId <= 0)
            return;

        string? fuentesJson = null;
        if (dto.Fuentes is { Count: > 0 })
            fuentesJson = JsonSerializer.Serialize(dto.Fuentes);

        await _incidencias.CrearAsync(new VisionIncidencias
        {
            UsuarioId = usuarioId,
            ConversacionId = conversacionId is > 0 ? conversacionId : null,
            Cultivo = dto.Cultivo,
            ClavePredicha = dto.Detecciones.FirstOrDefault()?.Clave,
            NombrePredicho = dto.ClasePredicha,
            Confianza = dto.ConfianzaPrincipal,
            Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Incierta" : dto.Estado,
            ModeloVersion = modeloVersion,
            ResumenVision = dto.Resumen,
            RespuestaAsistente = dto.RespuestaAsistente,
            FuentesJson = fuentesJson
        }, ct);
    }

    private static VisionIncidenciaDto MapIncidencia(VisionIncidencias x)
    {
        var fuentes = new List<string>();
        if (!string.IsNullOrWhiteSpace(x.FuentesJson))
        {
            try
            {
                fuentes = JsonSerializer.Deserialize<List<string>>(x.FuentesJson) ?? new List<string>();
            }
            catch
            {
                // ignore JSON inválido
            }
        }

        return new VisionIncidenciaDto
        {
            Id = x.Id,
            ConversacionId = x.ConversacionId,
            Cultivo = x.Cultivo,
            RutaImagen = x.RutaImagen,
            ClavePredicha = x.ClavePredicha,
            NombrePredicho = x.NombrePredicho,
            Confianza = x.Confianza,
            Estado = x.Estado,
            ModeloVersion = x.ModeloVersion,
            ResumenVision = x.ResumenVision,
            RespuestaAsistente = x.RespuestaAsistente,
            Fuentes = fuentes,
            FechaCreacion = x.FechaCreacion
        };
    }

    private async Task<string> GenerarRespuestaAsistenteAsync(
        string resumenVision,
        string estado,
        double? confianza,
        string cultivo,
        DeteccionVision? principal,
        IReadOnlyList<ContextoRecuperado> contextos,
        CancellationToken ct)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("RESULTADO DE VISIÓN:");
            sb.AppendLine(resumenVision);
            sb.AppendLine($"Estado: {estado}");
            if (confianza is not null)
                sb.AppendLine($"Confianza numérica: {confianza.Value:P0}");
            sb.AppendLine($"Cultivo de trabajo: {cultivo}");
            if (principal is not null)
                sb.AppendLine($"Clase modelo: {principal.Etiqueta.Nombre} ({principal.Etiqueta.Clave})");

            sb.AppendLine();
            if (contextos.Count == 0)
            {
                sb.AppendLine("CONTEXTO DE MANUALES: (vacío — no hay fragmentos relevantes)");
            }
            else
            {
                sb.AppendLine("CONTEXTO DE MANUALES:");
                for (int i = 0; i < contextos.Count; i++)
                {
                    var c = contextos[i];
                    sb.Append('[').Append(i + 1).Append("] ").Append(c.Titulo);
                    if (!string.IsNullOrWhiteSpace(c.Seccion)) sb.Append(" | ").Append(c.Seccion);
                    if (c.Pagina is not null) sb.Append(" | pág. ").Append(c.Pagina);
                    sb.AppendLine().AppendLine(c.Texto).AppendLine();
                }
            }

            sb.AppendLine("PREGUNTA DEL USUARIO:");
            sb.AppendLine("¿Qué tiene esta planta según la imagen y cómo puedo tratarla según los manuales?");
            sb.AppendLine();
            sb.AppendLine("Responde como en un chat profesional: claro, con incertidumbre si aplica, y solo manejo respaldado por el contexto.");

            var prompt = new ChatPrompt(SistemaVisionRag, sb.ToString(), "preciso");
            var result = await _chat.GenerarAsync(prompt, ct);

            if (result.Exito && !string.IsNullOrWhiteSpace(result.Respuesta))
                return result.Respuesta.Trim();

            return FallbackSinLlm(resumenVision, contextos);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "LLM no disponible en diagnóstico; se usa fallback");
            return FallbackSinLlm(resumenVision, contextos);
        }
    }

    private static string FallbackSinLlm(string resumen, IReadOnlyList<ContextoRecuperado> contextos)
    {
        var sb = new StringBuilder();
        sb.AppendLine(resumen);
        sb.AppendLine();
        if (contextos.Count == 0)
        {
            sb.AppendLine("No encontré fragmentos suficientes en los manuales para detallar el manejo. Consulta con un técnico de tu zona.");
            return sb.ToString();
        }

        sb.AppendLine("Según los manuales técnicos:");
        for (int i = 0; i < Math.Min(2, contextos.Count); i++)
        {
            var t = contextos[i].Texto;
            if (t.Length > 500) t = t[..500] + "…";
            sb.AppendLine($"[{i + 1}] {t}");
        }
        return sb.ToString();
    }

    private static string ResolverEstado(ResultadoVision r, DeteccionVision? principal, double? confianza)
    {
        if (!r.ModeloDisponible) return "SinModelo";
        if (principal is null) return "SinDeteccion";
        if (confianza < 0.45) return "Incierta";
        if (principal.Etiqueta.Tipo == TipoHallazgo.Sano) return "Identificada";
        if (string.Equals(principal.Etiqueta.Clave, "papa_otra_o_incierta", StringComparison.OrdinalIgnoreCase))
            return "Incierta";
        return "Identificada";
    }

    private static string ConstruirResumenVision(
        ResultadoVision r, DeteccionVision? principal, string estado, double? confianza)
    {
        if (!r.ModeloDisponible)
            return "El modelo de reconocimiento no está cargado; solo se evaluó daño aparente por color. No hay clasificación de enfermedad.";

        if (principal is null || estado is "SinDeteccion" or "Incierta")
        {
            var baseTxt = principal is null
                ? "No se identificó una plaga o enfermedad con suficiente confianza."
                : $"Posible coincidencia: {principal.Etiqueta.Nombre}" +
                  (confianza is not null ? $" ({confianza.Value * 100:0}% confianza)" : "") +
                  " — no es confirmación de laboratorio.";
            return baseTxt + " Se recomienda otra foto más cercana, buena luz, o revisión con un técnico.";
        }

        var pct = confianza is not null ? $" ({confianza.Value * 100:0}% confianza)" : "";
        var cult = string.IsNullOrWhiteSpace(principal.Etiqueta.Cultivo)
            ? ""
            : $" en {principal.Etiqueta.Cultivo}";

        return principal.Etiqueta.Tipo switch
        {
            TipoHallazgo.Sano => $"La planta parece sana{cult}{pct}. No es un dictamen definitivo.",
            TipoHallazgo.Plaga => $"Posible plaga: {principal.Etiqueta.Nombre}{cult}{pct} — no es confirmación de laboratorio.",
            TipoHallazgo.Enfermedad => $"Posible enfermedad: {principal.Etiqueta.Nombre}{cult}{pct} — no es confirmación de laboratorio.",
            _ => $"Posible hallazgo: {principal.Etiqueta.Nombre}{cult}{pct}."
        };
    }

    private static string ConstruirPreguntaRag(DeteccionVision? principal, string cultivo, string estado)
    {
        if (principal is null || estado is "SinModelo" or "SinDeteccion")
            return $"enfermedades y plagas frecuentes en {cultivo} síntomas en hojas manejo integrado";

        var nombre = principal.Etiqueta.Nombre;
        if (nombre.Contains("tardío", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("tardio", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("lanosa", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("lancha", StringComparison.OrdinalIgnoreCase))
        {
            return $"tizón tardío lancha lanosa Phytophthora en {cultivo} síntomas causas manejo cultural biológico fungicida";
        }

        return $"cómo se identifica y se maneja {nombre} en {cultivo} síntomas control integrado";
    }

    private static string? NormalizarCultivo(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return null;
        var t = c.Trim().ToLowerInvariant();
        if (t is "potato" or "papas") return "papa";
        if (t is "quinoa") return "quinua";
        if (t is "maíz" or "corn") return "maiz";
        return t;
    }

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

    private static string Recortar(string texto, int max)
    {
        texto = texto.Trim();
        return texto.Length <= max ? texto : texto[..max].TrimEnd() + "…";
    }
}