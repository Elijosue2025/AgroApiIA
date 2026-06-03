namespace AgroGuia.Aplicacion.DTO.DTOS.Auth
{
    public class LoginResponseDto
    {
        public bool Exito { get; set; }

        public string Mensaje { get; set; } = string.Empty;

        public long UsuarioId { get; set; }

        public string NombreCompleto { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Rol { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;
    }
}