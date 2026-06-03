using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones
{
    public interface IConversacionRepositorio
    {
        Task<Conversaciones> ConversacionCrearAsync(
            long usuarioId,
            string titulo);

        Task<Conversaciones?> ConversacionObtenerConMensajesAsync(
            long id);

        Task<List<Conversaciones>> ConversacionObtenerPorUsuarioAsync(
            long usuarioId);

        Task MensajeGuardarAsync(Mensajes mensaje);

        Task<List<string>> ChunksBuscarRelevantesAsync(
            string consulta,
            int topK = 4);

        Task<List<string>> BuscarChunksRelevantesAsync(
            string preguntaUsuario);
    }
}