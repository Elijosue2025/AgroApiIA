using System.Collections.Generic;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed record DocumentoExtraido(string Titulo, IReadOnlyList<PaginaExtraida> Paginas);
}
