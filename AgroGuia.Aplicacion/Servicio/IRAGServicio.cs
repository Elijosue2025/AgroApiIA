public interface IRAGServicio
{
    Task<List<string>> ObtenerContextoRelevanteAsync(
        string consulta);
}