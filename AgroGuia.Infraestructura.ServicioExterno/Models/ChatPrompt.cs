public sealed class ChatPrompt
{
    public string Sistema { get; init; } = "";
    public string Usuario { get; init; } = "";
    public string? ModoModelo { get; init; }

    public ChatPrompt(string sistema, string usuario, string? modoModelo = null)
    {
        Sistema = sistema;
        Usuario = usuario;
        ModoModelo = modoModelo;
    }
}