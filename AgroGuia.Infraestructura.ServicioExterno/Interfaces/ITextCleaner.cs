using System.Collections.Generic;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface ITextCleaner
    {
        IReadOnlyList<BloqueTexto> Limpiar(DocumentoExtraido documento);
    }
}
