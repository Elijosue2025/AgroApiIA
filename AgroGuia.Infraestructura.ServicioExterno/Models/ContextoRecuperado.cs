namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed record ContextoRecuperado(string Texto, string Titulo, string? Seccion, int? Pagina, double Score);
}
