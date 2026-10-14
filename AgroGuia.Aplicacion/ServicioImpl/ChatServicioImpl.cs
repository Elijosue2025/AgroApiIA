using AgroGuia.Aplicacion.DTO.DTOS.Chat;
using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class ChatServicioImpl : IChatServicio
{
    private readonly IRAGEngine _ragEngine;
    private readonly IConversacionRepositorio _conversacionRepositorio;

    public ChatServicioImpl(
        IRAGEngine ragEngine,
        IConversacionRepositorio conversacionRepositorio)
    {
        _ragEngine = ragEngine ?? throw new ArgumentNullException(nameof(ragEngine));
        _conversacionRepositorio = conversacionRepositorio
            ?? throw new ArgumentNullException(nameof(conversacionRepositorio));
    }

    public async Task<ChatResponseDto> ProcesarConsultaAsync(ConsultaRequestDto request)
    {
        try
        {
            var rag = await _ragEngine.ProcesarConsultaAsync(request.Consulta);

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
                Contenido = rag.Respuesta,
                Fecha = DateTime.UtcNow,
                Tokens = 0
            });

            return new ChatResponseDto
            {
                Exito = rag.Exito,
                Respuesta = rag.Respuesta,
                TokensUsados = 0,
                FechaRespuesta = DateTime.UtcNow,
                ErrorMensaje = rag.ErrorMensaje ?? ""
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