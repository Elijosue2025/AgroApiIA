namespace AgroGuia.Aplicacion.DTO.DTOS.Usuarios
{
    public class UsuarioDTO
    {
        public long Id { get; set; }

        public string NombreCompleto { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Rol { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; }

        public bool Activo { get; set; }
    }
}
