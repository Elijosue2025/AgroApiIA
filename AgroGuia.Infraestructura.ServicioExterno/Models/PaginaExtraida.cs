using System.Collections.Generic;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed record PaginaExtraida(int Numero, IReadOnlyList<string> Bloques);
}
