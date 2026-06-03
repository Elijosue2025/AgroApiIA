using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces;

public interface IOpenAIService
{
    Task<OpenAIChatResponse> ObtenerRespuestaConRAGAsync(
    string mensajeUsuario,
    List<string> chunksRelevantes);
}