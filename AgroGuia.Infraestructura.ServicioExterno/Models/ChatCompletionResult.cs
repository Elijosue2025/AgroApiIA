namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed class ChatCompletionResult
    {
        public bool Exito { get; init; }
        public string Respuesta { get; init; } = string.Empty;
        public string ErrorMensaje { get; init; } = string.Empty;
        public int TokensEntrada { get; init; }
        public int TokensSalida { get; init; }
        public int TokensTotales => TokensEntrada + TokensSalida;
    }
}
