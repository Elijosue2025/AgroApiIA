using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Aplicacion.Servicio
{
    public interface IRAGServicio
    {
        Task<List<string>> ObtenerContextoRelevanteAsync(string consulta);
        Task<RAGResponse> ProcesarConsultaCompletaAsync(string consulta);
    }
}