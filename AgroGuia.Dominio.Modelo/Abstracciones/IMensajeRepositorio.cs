using AgroGuia.Dominio.Modelo.Entidades;

namespace AgroGuia.Dominio.Modelo.Abstracciones
{
    public interface IMensajeRepositorio
    {
        Task CrearMensajeAsync(Mensajes mensaje);

        Task<List<Mensajes>> ObtenerMensajesConversacionAsync(long conversacionId);
    }
}