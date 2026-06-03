using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class ChatServicioImpl : IChatServicio
{
    private readonly IOpenAIService _openAIService;
    private readonly IConversacionRepositorio _conversacionRepositorio;

    public ChatServicioImpl(
        IOpenAIService openAIService,
        IConversacionRepositorio conversacionRepositorio)
    {
        _openAIService = openAIService ?? throw new ArgumentNullException(nameof(openAIService));
        _conversacionRepositorio = conversacionRepositorio ?? throw new ArgumentNullException(nameof(conversacionRepositorio));
    }

    public async Task<ChatResponseDto> ProcesarConsultaAsync(ConsultaRequestDto request)
    {
        try
        {
            // 1. Obtener chunks relevantes desde EmbeddingChunks
            List<string> chunks = await _conversacionRepositorio
                .ChunksBuscarRelevantesAsync(request.Consulta, topK: 5);

            if (chunks == null || !chunks.Any())
            {
                chunks = new List<string> { "No se encontró información relevante en los manuales técnicos." };
            }

            // 2. Obtener respuesta de Ollama (principal)
            var respuestaIA = await _openAIService
                .ObtenerRespuestaConRAGAsync(request.Consulta, chunks);

            // 3. Guardar mensaje del usuario
            await _conversacionRepositorio.MensajeGuardarAsync(new Mensajes
            {
                ConversacionId = request.ConversacionId,
                Rol = "user",
                Contenido = request.Consulta,
                Fecha = DateTime.UtcNow
            });

            // 4. Guardar respuesta del asistente
            await _conversacionRepositorio.MensajeGuardarAsync(new Mensajes
            {
                ConversacionId = request.ConversacionId,
                Rol = "assistant",
                Contenido = respuestaIA.Respuesta,
                Fecha = DateTime.UtcNow,
                Tokens = respuestaIA.TokensTotales
            });

            return new ChatResponseDto
            {
                Exito = true,
                Respuesta = respuestaIA.Respuesta,
                TokensUsados = respuestaIA.TokensTotales,
                FechaRespuesta = DateTime.UtcNow,
                ErrorMensaje = respuestaIA.ErrorMensaje ?? ""
            };
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
}