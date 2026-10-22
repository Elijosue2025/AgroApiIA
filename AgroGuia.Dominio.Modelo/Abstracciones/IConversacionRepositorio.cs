using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones;

public interface IConversacionRepositorio
{
    // ===== Gestión de conversaciones (chat tipo ChatGPT) =====
    Task<Conversaciones> CrearAsync(Conversaciones entidad);
    Task<Conversaciones?> ObtenerPorIdAsync(long id);
    Task<IReadOnlyList<Conversaciones>> ListarPorUsuarioAsync(long usuarioId, bool soloActivas = true);
    Task<IReadOnlyList<Mensajes>> ListarMensajesAsync(long conversacionId, int take = 200);
    Task ActualizarAsync(Conversaciones entidad);

    Task<IReadOnlyList<Conversaciones>> AdminListarAsync(
        long? usuarioId, DateTime? desde, DateTime? hasta, string? tituloContiene, int take = 50);

    // ===== Lo que YA usa el chat / mensajes =====
    Task MensajeGuardarAsync(Mensajes mensaje);

    // ===== Legacy (opcional: mantener hasta migrar el servicio) =====
    Task<Conversaciones> ConversacionCrearAsync(long usuarioId, string titulo);
    Task<Conversaciones?> ConversacionObtenerConMensajesAsync(long id);
    Task<List<Conversaciones>> ConversacionObtenerPorUsuarioAsync(long usuarioId);

    // ===== Chunks: mejor moverlos a IEmbeddingRepositorio a medio plazo =====
    // Si aún los usa ChatServicioImpl o el repo, déjalos por ahora:
    Task<List<string>> ChunksBuscarRelevantesAsync(string consulta, int topK = 4);
    Task<List<string>> BuscarChunksRelevantesAsync(string preguntaUsuario);
}