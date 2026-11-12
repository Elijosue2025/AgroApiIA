namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    public interface ICatalogoAgronomico
    {
        string? DetectarCultivo(string texto, string? titulo = null);
        string? DetectarTema(string texto, string? titulo = null);
        string? DetectarPlagaOEnfermedad(string texto, string? titulo = null);
        void Invalidar();
    }
}