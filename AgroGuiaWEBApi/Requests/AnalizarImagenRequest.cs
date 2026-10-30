namespace AgroGuiaWEBApi.Requests
{
    /// <summary>Formulario multipart/form-data de POST api/Diagnostico/analizar.</summary>
    public class AnalizarImagenRequest
    {
        public IFormFile? Imagen { get; set; }
        public string? Cultivo { get; set; }
        public long? ConversacionId { get; set; }
    }
}
