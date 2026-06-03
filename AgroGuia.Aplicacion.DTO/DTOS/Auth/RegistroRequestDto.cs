namespace AgroGuia.Aplicacion.DTO.DTOS.Auth
{
    public class RegistroRequestDto
    {
        public string NombreCompleto { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;
    }
}
