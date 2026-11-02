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
        Eres AgroGuía IA, asistente agronómico. El usuario envió una FOTO y un análisis visual automático.
        REGLAS:
        1. Usa SOLO el CONTEXTO de manuales y el RESULTADO DE VISIÓN. No inventes plagas, dosis ni marcas.
        2. Si el estado es SinModelo, SinDeteccion o Incierta, no pongas nombre de plaga. Pide mejor foto o un técnico.
        3. La condición (Baja, Moderada, Alta) describe el daño visible, no el nombre de la enfermedad.
        4. Si hay clase identificada, el manejo sale del contexto: monitoreo, cultural, biológico y productos solo si el fragmento los menciona.
        5. Cita con [1], [2] según el orden del CONTEXTO.
        6. Español claro, sin relleno.
        """;

    private const int MaxCaracteresRecomendacion = 700;

    private readonly IAnalizadorVision _vision;
    private readonly ContextRetriever _contextRetriever;
    private readonly IChatCompletionService _chat;
    private readonly IVisionIncidenciaRepositorio _incidencias;
    private readonly IConversacionRepositorio _conversaciones;
    private readonly VisionOptions _opt;
    private readonly ILogger<DiagnosticoServicioImpl> _log;

    public DiagnosticoServicioImpl(
        IAnalizadorVision vision,
        ContextRetriever contextRetriever,
        IChatCompletionService chat,
        IVisionIncidenciaRepositorio incidencias,
        IConversacionRepositorio conversaciones,
        IOptions<VisionOptions> opt,
        ILogger<DiagnosticoServicioImpl> log)
    {
        _vision = vision;
        _contextRetriever = contextRetriever;
        _chat = chat;
        _incidencias = incidencias;
        _conversaciones = conversaciones;
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
        byte[] imagen,
        string? cultivo = null,
        long usuarioId = 0,
        long? conversacionId = null,
        string? consulta = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var cultivoNorm = NormalizarCultivo(cultivo) ?? "no indicado";
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
                Cultivo = cultivoNorm,
                RespuestaAsistente = r.ErrorMensaje
            };
            await GuardarSeguroAsync(usuarioId, conversacionId, errorDto, consulta, ct);
            return errorDto;
        }

        var principal = r.Detecciones.FirstOrDefault();
        var confianza = principal is null ? (double?)null : principal.Confianza;
        var estado = ResolverEstado(r, principal, confianza);
        var condicion = r.Lesiones is { TejidoVegetalDetectado: true }
            ? r.Lesiones.Severidad
            : "indeterminada";

        var dto = new DiagnosticoImagenResponseDto
        {
            Exito = true,
            ModeloDisponible = r.ModeloDisponible,
            Detecciones = r.Detecciones.Select(Mapear).ToList(),
            Lesiones = r.Lesiones is null ? null : Mapear(r.Lesiones),
            Advertencia = AdvertenciaBase,
            FechaRespuesta = DateTime.UtcNow,
            Estado = estado,
            ConfianzaPrincipal = confianza,
            ClasePredicha = principal?.Etiqueta.Nombre,
            Cultivo = principal?.Etiqueta.Cultivo ?? cultivoNorm,
            Resumen = ConstruirResumenVision(r, principal, estado, confianza, condicion)
        };

        var preguntaRag = ConstruirPreguntaRag(dto.Cultivo!, estado, condicion, principal, consulta);
        IReadOnlyList<ContextoRecuperado> contextos = Array.Empty<ContextoRecuperado>();
        try
        {
            if (_opt.IncluirRecomendacionesRag)
                contextos = await _contextRetriever.ObtenerContextoAsync(preguntaRag, ct);
        }
        catch (OperationCanceledException) { throw; }
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

        dto.Fuentes = contextos.Select((c, i) =>
        {
            var p = c.Pagina is null ? "" : $", pág. {c.Pagina}";
            var sec = string.IsNullOrWhiteSpace(c.Seccion) ? "" : $" — {c.Seccion}";
            return $"[{i + 1}] {c.Titulo}{sec}{p}";
        }).ToList();

        dto.RespuestaAsistente = await GenerarRespuestaAsistenteAsync(
            dto.Resumen, estado, confianza, dto.Cultivo!, condicion, principal, consulta, contextos, ct);

        await GuardarSeguroAsync(usuarioId, conversacionId, dto, consulta, ct);
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
        if (x is null || x.UsuarioId != usuarioId) return null;
        return MapIncidencia(x);
    }

    private async Task GuardarSeguroAsync(
        long usuarioId, long? conversacionId, DiagnosticoImagenResponseDto dto, string? consulta, CancellationToken ct)
    {
        try { await GuardarIncidenciaAsync(usuarioId, conversacionId, dto, ct); }
        catch (Exception ex) { _log.LogWarning(ex, "No se pudo guardar la incidencia de visión"); }

        try { await GuardarEnChatAsync(conversacionId, dto, consulta); }
        catch (Exception ex) { _log.LogWarning(ex, "No se pudo guardar la foto en el hilo del chat"); }
    }

    private async Task GuardarIncidenciaAsync(
        long usuarioId, long? conversacionId, DiagnosticoImagenResponseDto dto, CancellationToken ct)
    {
        if (usuarioId <= 0) return;
        await _incidencias.CrearAsync(new VisionIncidencias
        {
            UsuarioId = usuarioId,
            ConversacionId = conversacionId is > 0 ? conversacionId : null,
            Cultivo = dto.Cultivo,
            ClavePredicha = dto.Detecciones.FirstOrDefault()?.Clave,
            NombrePredicho = dto.ClasePredicha,
            Confianza = dto.ConfianzaPrincipal,
            Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Incierta" : dto.Estado,
            ResumenVision = dto.Resumen,
            RespuestaAsistente = dto.RespuestaAsistente,
            FuentesJson = dto.Fuentes is { Count: > 0 } ? JsonSerializer.Serialize(dto.Fuentes) : null
        }, ct);
    }

    private async Task GuardarEnChatAsync(long? conversacionId, DiagnosticoImagenResponseDto dto, string? consulta)
    {
        if (conversacionId is not > 0 || string.IsNullOrWhiteSpace(dto.RespuestaAsistente)) return;

        var textoUsuario = string.IsNullOrWhiteSpace(consulta)
            ? $"[Foto] Cultivo: {dto.Cultivo}."
            : consulta.Trim();

        await _conversaciones.MensajeGuardarAsync(new Mensajes
        {
            ConversacionId = conversacionId.Value,
            Rol = "user",
            Contenido = textoUsuario,
            Fecha = DateTime.UtcNow
        });
        await _conversaciones.MensajeGuardarAsync(new Mensajes
        {
            ConversacionId = conversacionId.Value,
            Rol = "assistant",
            Contenido = dto.RespuestaAsistente,
            Fecha = DateTime.UtcNow
        });
    }

    private static VisionIncidenciaDto MapIncidencia(VisionIncidencias x)
    {
        var fuentes = new List<string>();
        if (!string.IsNullOrWhiteSpace(x.FuentesJson))
        {
            try { fuentes = JsonSerializer.Deserialize<List<string>>(x.FuentesJson) ?? new(); }
            catch { /* JSON viejo */ }
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
        string resumenVision, string estado, double? confianza, string cultivo, string condicion,
        DeteccionVision? principal, string? consulta, IReadOnlyList<ContextoRecuperado> contextos, CancellationToken ct)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("RESULTADO DE VISIÓN:");
            sb.AppendLine(resumenVision);
            sb.AppendLine($"Estado: {estado}");
            sb.AppendLine($"Cultivo: {cultivo}");
            sb.AppendLine($"Condición de la hoja: {condicion}");
            if (confianza is not null) sb.AppendLine($"Confianza: {confianza.Value:P0}");
            if (principal is not null)
                sb.AppendLine($"Clase: {principal.Etiqueta.Nombre}");
            sb.AppendLine();
            if (contextos.Count == 0)
                sb.AppendLine("CONTEXTO DE MANUALES: (vacío)");
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
            sb.AppendLine("PREGUNTA:");
            sb.AppendLine(string.IsNullOrWhiteSpace(consulta)
                ? "¿Qué tiene esta planta y cómo la trato según los manuales?"
                : consulta.Trim());

            var prompt = new ChatPrompt(SistemaVisionRag, sb.ToString(), "preciso");
            var result = await _chat.GenerarAsync(prompt, ct);
            if (result.Exito && !string.IsNullOrWhiteSpace(result.Respuesta))
                return result.Respuesta.Trim();
            return FallbackSinLlm(resumenVision, estado, contextos);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "LLM no disponible en diagnóstico; se usa fallback");
            return FallbackSinLlm(resumenVision, estado, contextos);
        }
    }

    private static string FallbackSinLlm(string resumen, string estado, IReadOnlyList<ContextoRecuperado> contextos)
    {
        var sb = new StringBuilder();
        sb.AppendLine(resumen);
        if (estado is "SinModelo" or "SinDeteccion" or "Incierta")
            sb.AppendLine("No se identificó la afección. Consulta con un técnico antes de aplicar un producto.");
        if (contextos.Count == 0) return sb.ToString().Trim();
        sb.AppendLine("Según los manuales:");
        for (int i = 0; i < Math.Min(2, contextos.Count); i++)
        {
            var t = contextos[i].Texto;
            if (t.Length > 500) t = t[..500] + "…";
            sb.AppendLine($"[{i + 1}] {t}");
        }
        return sb.ToString().Trim();
    }

    private string ResolverEstado(ResultadoVision r, DeteccionVision? principal, double? confianza)
    {
        if (!r.ModeloDisponible) return "SinModelo";
        if (principal is null || confianza is null || confianza < _opt.ConfianzaMinima) return "Incierta";
        if (string.Equals(principal.Etiqueta.Clave, "papa_otra_o_incierta", StringComparison.OrdinalIgnoreCase))
            return "Incierta";
        return "Identificada";
    }

    private static string ConstruirResumenVision(
        ResultadoVision r, DeteccionVision? principal, string estado, double? confianza, string condicion)
    {
        var dano = condicion == "indeterminada"
            ? "No se pudo estimar el daño en la hoja."
            : $"Condición de la hoja: {condicion} ({r.Lesiones!.PorcentajeAfectado:0.#}% del tejido, {r.Lesiones.NumeroManchas} manchas).";

        if (!r.ModeloDisponible)
            return "No hay modelo de plagas cargado, así que no se nombra la enfermedad. " + dano;

        if (principal is null || estado is "Incierta")
            return "No hay una clase con suficiente confianza. " + dano + " Conviene otra foto o un técnico.";

        var pct = confianza is not null ? $" ({confianza.Value * 100:0}% de confianza)" : "";
        return $"Posible {principal.Etiqueta.Nombre}{pct}. " + dano + " No es confirmación de laboratorio.";
    }

    private static string ConstruirPreguntaRag(
        string cultivo, string estado, string condicion, DeteccionVision? principal, string? consulta)
    {
        var extra = string.IsNullOrWhiteSpace(consulta) ? "" : " " + consulta.Trim();
        if (principal is null || estado is "SinModelo" or "Incierta")
            return $"Cultivo: {cultivo}. La imagen no identificó la afección. Condición de la hoja: {condicion}.{extra} No atribuir una plaga.";

        return $"Cultivo: {cultivo}. Estado: {principal.Etiqueta.Nombre}. Condición: {condicion}. Manejo de {principal.Etiqueta.Nombre} en {cultivo}.{extra}";
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