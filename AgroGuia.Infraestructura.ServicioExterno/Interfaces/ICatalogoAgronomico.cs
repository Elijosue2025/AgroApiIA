namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface ICatalogoAgronomico
    {
        /// <summary>null si no hay ninguna coincidencia. 'titulo' pesa x5.</summary>
        string? DetectarCultivo(string texto, string? titulo = null);
        string? DetectarTema(string texto, string? titulo = null);
    }
}
