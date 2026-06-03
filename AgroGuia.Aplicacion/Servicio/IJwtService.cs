namespace AgroGuia.Aplicacion.Servicio
{
    public interface IJwtService
    {
        string GenerarToken(
            long idUsuario,
            string nombre,
            string email);
    }
}