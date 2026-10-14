using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface IPdfTextExtractor
    {
        DocumentoExtraido Extraer(string rutaPdf);
    }
}
