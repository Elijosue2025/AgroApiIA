using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Aplicacion.ServicioImpl
{
    public class RAGServicioImpl : IRAGServicio
    {
        private readonly IRAGEngine _ragEngine;

        public RAGServicioImpl(IRAGEngine ragEngine)
        {
            _ragEngine = ragEngine;
        }

        public async Task<List<string>> ObtenerContextoRelevanteAsync(string consulta)
        {
            if (string.IsNullOrWhiteSpace(consulta))
                return new List<string>();

            var resultado = await _ragEngine.ProcesarConsultaAsync(consulta);

            return resultado.ChunksUtilizados ?? new List<string>();
        }

        public async Task<RAGResponse> ProcesarConsultaCompletaAsync(string consulta)
        {
            if (string.IsNullOrWhiteSpace(consulta))
                return new RAGResponse
                {
                    Exito = false,
                    ErrorMensaje = "La consulta está vacía."
                };

            return await _ragEngine.ProcesarConsultaAsync(consulta);
        }
    }
}