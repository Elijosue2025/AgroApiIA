namespace AgroGuiaWEBApi.Requests
{
    /// <summary>Formulario multipart/form-data de POST api/Diagnostico/analizar.</summary>
    public class AnalizarImagenRequest
    {
        /// <summary>Foto de la planta (campo "imagen"): JPG, PNG, WebP o BMP.</summary>
        public IFormFile? Imagen { get; set; }

        /// <summary>Cultivo, si el usuario lo conoce (ej. "Papa"). Opcional.</summary>
        public string? Cultivo { get; set; }
    }
}
