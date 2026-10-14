using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.RAG;

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
            // 1) Contexto desde embeddings + índice (NO SQL uno a uno)
            var contextos = await _contextRetriever.ObtenerContextoAsync(request.Consulta);

            if (contextos.Count == 0)
            {
                const string sinInfo =
                    "No encontré información suficiente en los manuales técnicos sobre este tema.";
                await GuardarParAsync(request, sinInfo, 0);
                return Ok(sinInfo, 0);
            }

            // 2) Prompt con SOLO contexto recuperado
            var prompt = _promptBuilder.Construir(request.Consulta, contextos);
            // (ajusta el nombre del método a como esté en tu PromptBuilder)

            // 3) LLM (Ollama por defecto)
            var result = await _chat.GenerarAsync(prompt);

            var texto = result.Exito && !string.IsNullOrWhiteSpace(result.Respuesta)
                ? result.Respuesta
                : "No encontré información suficiente en los manuales técnicos sobre este tema.";

            var tokens = result.TokensEntrada + result.TokensSalida;
            await GuardarParAsync(request, texto, tokens);

            return Ok(texto, tokens, result.ErrorMensaje);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR ChatServicio] {ex.Message}");
            return new ChatResponseDto
            {
                Exito = false,
                Respuesta = "Lo siento, ocurrió un error interno al procesar tu consulta.",
                ErrorMensaje = ex.Message
            };
        }
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