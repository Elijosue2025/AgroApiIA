// AgroGuia.Aplicacion/ServicioImpl/ChatServicioImpl.cs
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.RAG;
using Microsoft.Extensions.Logging;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class ChatServicioImpl : IChatServicio
{
    private const int MaxTurnosHistorial = 8; // 8 mensajes ≈ 4 pares user/assistant
    private const int MaxCharsHistorial = 1800;

    private readonly ContextRetriever _contextRetriever;
    private readonly PromptBuilder _promptBuilder;
    private readonly IChatCompletionService _chat;
    private readonly IConversacionRepositorio _conversacionRepositorio;
    private readonly ILogger<ChatServicioImpl> _log;

    // Cultivos frecuentes (amplía según tu catálogo)
    private static readonly string[] CultivosConocidos =
    {
        "quinua", "quinoa", "papa", "maíz", "maiz", "frejol", "fréjol", "frijol",
        "trigo", "cebada", "haba", "arveja", "ulluco", "oca", "mashua", "tomate",
        "cebolla", "ajo", "lenteja", "chocho", "amaranto"
    };

    public ChatServicioImpl(
        ContextRetriever contextRetriever,
        PromptBuilder promptBuilder,
        IChatCompletionService chat,
        IConversacionRepositorio conversacionRepositorio,
        ILogger<ChatServicioImpl> log)
    {
        _contextRetriever = contextRetriever;
        _promptBuilder = promptBuilder;
        _chat = chat;
        _conversacionRepositorio = conversacionRepositorio;
        _log = log;
    }

    public async Task<ChatResponseDto> ProcesarConsultaAsync(ConsultaRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Consulta))
            {
                return new ChatResponseDto
                {
                    Exito = false,
                    Respuesta = "Escribe una consulta agronómica.",
                    FechaRespuesta = DateTime.UtcNow
                };
            }

            // ----- 0) Historial de ESTA conversación -----
            var previos = await CargarMensajesPreviosAsync(request.ConversacionId);
            var historialTexto = FormatearHistorial(previos);
            var consultaRag = AmpliarConsultaParaRag(request.Consulta, previos);

            _log.LogInformation(
                "Hilo conv={Id}: historial={N} msgs | ragQuery=\"{Q}\"",
                request.ConversacionId, previos.Count, consultaRag);

            // ----- 1) RAG con consulta ampliada (mantiene cultivo/tema del hilo) -----
            var reloj = Stopwatch.StartNew();
            var contextos = await _contextRetriever.ObtenerContextoAsync(consultaRag);
            var msContexto = reloj.ElapsedMilliseconds;

            if (contextos.Count == 0)
            {
                var sinInfo = PromptBuilder.RespuestaSinInformacion;
                await GuardarParAsync(request, sinInfo, 0);
                return Ok(sinInfo, 0);
            }

            // ----- 2) Prompt = historial + contexto + pregunta actual -----
            var usados = _promptBuilder.SeleccionarContexto(contextos);
            var basePrompt = _promptBuilder.Construir(request.Consulta.Trim(), usados, historialTexto);

            var prompt = new ChatPrompt(
                basePrompt.Sistema,
                basePrompt.Usuario,
                NormalizarModo(request.ModoModelo));

            // ----- 3) LLM -----
            reloj.Restart();
            var result = await _chat.GenerarAsync(prompt);

            _log.LogInformation(
                "Chat: contexto {MsCtx} ms ({Usados}/{Recuperados} frag.) | modelo {MsLlm} ms | modo {Modo}",
                msContexto, usados.Count, contextos.Count,
                reloj.ElapsedMilliseconds, prompt.ModoModelo ?? "(defecto)");

            string texto;
            if (result.Exito && !string.IsNullOrWhiteSpace(result.Respuesta))
            {
                texto = result.Respuesta.Trim();
            }
            else
            {
                texto = string.IsNullOrWhiteSpace(result.ErrorMensaje)
                    ? PromptBuilder.RespuestaSinInformacion
                    : "El asistente no pudo generar la respuesta. Intenta de nuevo o usa el modo Rápido.";
            }

            var tokens = result.TokensEntrada + result.TokensSalida;
            if (result.Exito)
                await GuardarParAsync(request, texto, tokens);

            // Opcional: persistir cultivo del hilo si la entidad/repo lo soportan
            await IntentarActualizarCultivoHiloAsync(request.ConversacionId, request.Consulta, previos);

            return Ok(texto, tokens, result.Exito ? null : result.ErrorMensaje);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error en ChatServicio");
            return new ChatResponseDto
            {
                Exito = false,
                Respuesta = "Lo siento, ocurrió un error interno al procesar tu consulta.",
                TokensUsados = 0,
                FechaRespuesta = DateTime.UtcNow,
                ErrorMensaje = ex.Message
            };
        }
    }

    // -------------------------------------------------------------------------
    // Historial
    // -------------------------------------------------------------------------

    private async Task<IReadOnlyList<Mensajes>> CargarMensajesPreviosAsync(long conversacionId)
    {
        if (conversacionId <= 0)
            return Array.Empty<Mensajes>();

        var list = await _conversacionRepositorio.ListarMensajesAsync(conversacionId, take: 50);

        return list
            .OrderBy(m => m.Fecha)
            .TakeLast(MaxTurnosHistorial)
            .ToList();
    }
    private static string? FormatearHistorial(IReadOnlyList<Mensajes> previos)
    {
        if (previos.Count == 0) return null;

        var sb = new StringBuilder();
        foreach (var m in previos)
        {
            var rol = string.Equals(m.Rol, "user", StringComparison.OrdinalIgnoreCase)
                ? "Usuario"
                : "Asistente";
            var contenido = (m.Contenido ?? "").Trim();
            if (contenido.Length > 400)
                contenido = contenido[..400].TrimEnd() + "…";
            sb.Append(rol).Append(": ").Append(contenido).Append('\n');
        }

        var texto = sb.ToString();
        if (texto.Length > MaxCharsHistorial)
            texto = texto[^MaxCharsHistorial..]; // cola del hilo (lo más reciente)

        return texto;
    }

    /// <summary>
    /// Si la pregunta es corta / seguimiento, añade cultivo y pistas del historial
    /// para que el RAG no busque solo "más métodos".
    /// </summary>
    private static string AmpliarConsultaParaRag(string preguntaActual, IReadOnlyList<Mensajes> previos)
    {
        var q = preguntaActual.Trim();
        if (previos.Count == 0) return q;

        var cultivoEnPregunta = DetectarCultivo(q);
        var cultivoHilo = cultivoEnPregunta
            ?? DetectarCultivoEnMensajes(previos)
            ?? "";

        var esSeguimiento = PareceSeguimiento(q) || cultivoEnPregunta is null;

        if (!esSeguimiento && cultivoEnPregunta is not null)
            return q; // pregunta nueva ya explícita

        // Últimas preguntas del usuario (pistas: trips, jabón, siembra…)
        var pistas = previos
            .Where(m => string.Equals(m.Rol, "user", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Contenido ?? "")
            .Reverse()
            .Take(3)
            .ToList();

        var extra = new StringBuilder(q);
        if (!string.IsNullOrEmpty(cultivoHilo) &&
            q.IndexOf(cultivoHilo, StringComparison.OrdinalIgnoreCase) < 0)
        {
            extra.Append(' ').Append(cultivoHilo);
        }

        // Añade 1–2 frases previas del user si el mensaje actual es muy corto
        if (q.Length < 80 || PareceSeguimiento(q))
        {
            foreach (var p in pistas)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (p.Equals(q, StringComparison.OrdinalIgnoreCase)) continue;
                extra.Append(' ').Append(p.Length > 120 ? p[..120] : p);
                break; // una pista basta para no ensuciar el embed
            }
        }

        return Regex.Replace(extra.ToString(), @"\s+", " ").Trim();
    }

    private static bool PareceSeguimiento(string q)
    {
        var t = q.Trim().ToLowerInvariant();
        if (t.Length <= 48) return true;
        return t.StartsWith("y ") || t.StartsWith("y el") || t.StartsWith("y la")
            || t.StartsWith("más ") || t.StartsWith("mas ")
            || t.Contains("otro método") || t.Contains("otros metodos")
            || t.Contains("más métodos") || t.Contains("mas metodos")
            || t.StartsWith("eso") || t.StartsWith("y si")
            || t.Contains("también") || t.Contains("tambien");
    }

    private static string? DetectarCultivo(string texto)
    {
        var t = texto.ToLowerInvariant();
        foreach (var c in CultivosConocidos)
            if (t.Contains(c)) return c is "quinoa" ? "quinua" : c is "maiz" ? "maíz" : c;
        return null;
    }

    private static string? DetectarCultivoEnMensajes(IReadOnlyList<Mensajes> msgs)
    {
        for (int i = msgs.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(msgs[i].Rol, "user", StringComparison.OrdinalIgnoreCase))
                continue;
            var c = DetectarCultivo(msgs[i].Contenido ?? "");
            if (c is not null) return c;
        }
        return null;
    }

    private async Task IntentarActualizarCultivoHiloAsync(
    long conversacionId, string pregunta, IReadOnlyList<Mensajes> previos)
    {
        if (conversacionId <= 0) return;

        var cultivo = DetectarCultivo(pregunta) ?? DetectarCultivoEnMensajes(previos);
        if (cultivo is null) return;

        var conv = await _conversacionRepositorio.ObtenerPorIdAsync(conversacionId);
        if (conv is null) return;

        // Ya tiene el mismo cultivo → no hace falta guardar
        if (string.Equals(conv.CultivoActual, cultivo, StringComparison.OrdinalIgnoreCase))
            return;

        conv.CultivoActual = cultivo;

        // Mejora el título solo si sigue siendo el por defecto
        var tituloActual = (conv.Titulo ?? "").Trim();
        if (string.IsNullOrEmpty(tituloActual) ||
            tituloActual.Equals("Nueva consulta", StringComparison.OrdinalIgnoreCase) ||
            tituloActual.Equals("Nueva Consulta Agronómica", StringComparison.OrdinalIgnoreCase))
        {
            // Primera letra en mayúscula
            var nombre = char.ToUpper(cultivo[0]) + cultivo[1..];
            conv.Titulo = $"Consulta sobre {nombre}";
        }

        await _conversacionRepositorio.ActualizarAsync(conv);
    }
    

    // -------------------------------------------------------------------------
    // Persistencia / helpers
    // -------------------------------------------------------------------------

    private static string? NormalizarModo(string? modo)
    {
        if (string.IsNullOrWhiteSpace(modo)) return null;
        modo = modo.Trim().ToLowerInvariant();
        return modo is "rapido" or "preciso" ? modo : null;
    }

    private static ChatResponseDto Ok(string respuesta, int tokens, string? error = null) => new()
    {
        Exito = true,
        Respuesta = respuesta,
        TokensUsados = tokens,
        FechaRespuesta = DateTime.UtcNow,
        ErrorMensaje = error ?? ""
    };

    private async Task GuardarParAsync(ConsultaRequestDto request, string respuesta, int tokens)
    {
        await _conversacionRepositorio.MensajeGuardarAsync(new Mensajes
        {
            ConversacionId = request.ConversacionId,
            Rol = "user",
            Contenido = request.Consulta,
            Fecha = DateTime.UtcNow
        });

        await _conversacionRepositorio.MensajeGuardarAsync(new Mensajes
        {
            ConversacionId = request.ConversacionId,
            Rol = "assistant",
            Contenido = respuesta,
            Fecha = DateTime.UtcNow,
            Tokens = tokens
        });
    }
}