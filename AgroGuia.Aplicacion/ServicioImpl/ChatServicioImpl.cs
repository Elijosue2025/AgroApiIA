using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using AgroGuia.Infraestructura.ServicioExterno.RAG;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class ChatServicioImpl : IChatServicio
{
    private readonly ContextRetriever _contextRetriever;
    private readonly PromptBuilder _promptBuilder;
    private readonly IChatCompletionService _chat;
    private readonly IConversacionRepositorio _conversacionRepositorio;

    public ChatServicioImpl(
        ContextRetriever contextRetriever,
        PromptBuilder promptBuilder,
        IChatCompletionService chat,
        IConversacionRepositorio conversacionRepositorio)
    {
        _contextRetriever = contextRetriever;
        _promptBuilder = promptBuilder;
        _chat = chat;
        _conversacionRepositorio = conversacionRepositorio;
    }

    public async Task<ChatResponseDto> ProcesarConsultaAsync(ConsultaRequestDto request)
    {
        try
        {
            // 1) Contexto desde embeddings + índice en memoria
            var contextos = await _contextRetriever.ObtenerContextoAsync(request.Consulta);

            if (contextos.Count == 0)
            {
                var sinInfo = PromptBuilder.RespuestaSinInformacion;
                await GuardarParAsync(request, sinInfo, 0);
                return Ok(sinInfo, 0);
            }

            // 2) Prompt con contexto recuperado
            var basePrompt = _promptBuilder.Construir(request.Consulta, contextos);

            // 3) Adjuntar modo de modelo (rapido | preciso | null)
            var prompt = new ChatPrompt(
                basePrompt.Sistema,
                basePrompt.Usuario,
                NormalizarModo(request.ModoModelo));

            // 4) LLM (Ollama)
            var result = await _chat.GenerarAsync(prompt);

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