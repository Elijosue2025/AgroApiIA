using System.Diagnostics;
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
    private readonly ContextRetriever _contextRetriever;
    private readonly PromptBuilder _promptBuilder;
    private readonly IChatCompletionService _chat;
    private readonly IConversacionRepositorio _conversacionRepositorio;
    private readonly ILogger<ChatServicioImpl> _log;

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
            // 1) Contexto desde embeddings + índice en memoria
            var reloj = Stopwatch.StartNew();
            var contextos = await _contextRetriever.ObtenerContextoAsync(request.Consulta);
            var msContexto = reloj.ElapsedMilliseconds;

            if (contextos.Count == 0)
            {
                var sinInfo = PromptBuilder.RespuestaSinInformacion;
                await GuardarParAsync(request, sinInfo, 0);
                return Ok(sinInfo, 0);
            }

            // 2) Presupuesto de contexto (Rag:ContextoMaxCaracteres). Antes solo lo aplicaba RagEngine, que este flujo
            //    no usa: con TopK 5 y vecinos el prompt podía llegar a ~13 000 caracteres en lugar de 3 500.
            var usados = _promptBuilder.SeleccionarContexto(contextos);
            var basePrompt = _promptBuilder.Construir(request.Consulta, usados);

            // 3) Adjuntar modo de modelo (rapido | preciso | null)
            var prompt = new ChatPrompt(
                basePrompt.Sistema,
                basePrompt.Usuario,
                NormalizarModo(request.ModoModelo));

            // 4) LLM (Ollama)
            reloj.Restart();
            var result = await _chat.GenerarAsync(prompt);

            _log.LogInformation(
                "Chat: contexto {MsCtx} ms ({Usados}/{Recuperados} fragmentos, {Car} car.) | modelo {MsLlm} ms | modo {Modo}",
                msContexto, usados.Count, contextos.Count, usados.Sum(u => u.Texto.Length),
                reloj.ElapsedMilliseconds, prompt.ModoModelo ?? "(defecto)");

            string texto;
            if (result.Exito && !string.IsNullOrWhiteSpace(result.Respuesta))
            {
                texto = result.Respuesta.Trim();
            }
            else
            {
                // No guardar un fallo del modelo como si fuera respuesta válida del manual
                texto = string.IsNullOrWhiteSpace(result.ErrorMensaje)
                    ? PromptBuilder.RespuestaSinInformacion
                    : "El asistente no pudo generar la respuesta. Intenta de nuevo o usa el modo Rápido.";
            }

            var tokens = result.TokensEntrada + result.TokensSalida;

            // Solo persistimos si hubo respuesta usable
            if (result.Exito)
                await GuardarParAsync(request, texto, tokens);

            return Ok(texto, tokens, result.Exito ? null : result.ErrorMensaje);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR ChatServicio] {ex.Message}");
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