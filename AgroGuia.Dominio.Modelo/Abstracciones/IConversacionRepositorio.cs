using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones;

public interface IConversacionRepositorio
{
    Task<Conversaciones> CrearAsync(Conversaciones entidad);
    Task<Conversaciones?> ObtenerPorIdAsync(long id);
    Task<IReadOnlyList<Conversaciones>> ListarPorUsuarioAsync(long usuarioId, bool soloActivas = true);
    Task<IReadOnlyList<Mensajes>> ListarMensajesAsync(long conversacionId, int take = 200);
    Task ActualizarAsync(Conversaciones entidad);

    Task<IReadOnlyList<Conversaciones>> AdminListarAsync(
        long? usuarioId,
        DateTime? desde,
        DateTime? hasta,
        string? tituloContiene,
        int take = 50);

    Task MensajeGuardarAsync(Mensajes mensaje);
}