using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones;

public interface IConversacionRepositorio
{
    // Gestión de conversaciones
    Task<Conversaciones> CrearAsync(Conversaciones entidad);
    Task<Conversaciones?> ObtenerPorIdAsync(long id);
    Task<IReadOnlyList<Conversaciones>> ListarPorUsuarioAsync(long usuarioId, bool soloActivas = true);
    Task<IReadOnlyList<Mensajes>> ListarMensajesAsync(long conversacionId, int take = 200);
    Task ActualizarAsync(Conversaciones entidad);

    Task<IReadOnlyList<Conversaciones>> AdminListarAsync(
        long? usuarioId, DateTime? desde, DateTime? hasta, string? tituloContiene, int take = 50);

    // Mensajes (una sola vez)
    Task MensajeGuardarAsync(Mensajes mensaje);

    // Legacy (una sola vez cada uno)
    Task<Conversaciones> ConversacionCrearAsync(long usuarioId, string titulo);
    Task<Conversaciones?> ConversacionObtenerConMensajesAsync(long id);
    Task<List<Conversaciones>> ConversacionObtenerPorUsuarioAsync(long usuarioId);

    // Chunks (si aún los usa el repo)
    Task<List<string>> ChunksBuscarRelevantesAsync(string consulta, int topK = 4);
    Task<List<string>> BuscarChunksRelevantesAsync(string preguntaUsuario);
}