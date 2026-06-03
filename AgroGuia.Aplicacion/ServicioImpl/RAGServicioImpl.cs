using AgroGuia.Dominio.Modelo.Abstracciones;

namespace AgroGuia.Aplicacion.ServicioImpl;

public class RAGServicioImpl : IRAGServicio
{
    private readonly IConversacionRepositorio _conversacionRepositorio;

    public RAGServicioImpl(IConversacionRepositorio conversacionRepositorio)
    {
        _conversacionRepositorio = conversacionRepositorio;
    }

    public async Task<List<string>> ObtenerContextoRelevanteAsync(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return new List<string>();

        return await _conversacionRepositorio
            .ChunksBuscarRelevantesAsync(consulta, topK: 4);
    }
}