namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces;

public interface IWebSearchService
{
    Task<List<string>> BuscarAsync(string consulta, int maxResultados = 3);
}