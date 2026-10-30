using Microsoft.AspNetCore.Http;

namespace AgroGuiaWEBApi.Requests;

public class AnalizarImagenRequest
{
    public IFormFile? Imagen { get; set; }
    public string? Cultivo { get; set; }
    public long? ConversacionId { get; set; }
}
