using System.Threading;
using System.Threading.Tasks;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Interfaces
{
    /// <summary>Antes IOpenAIService: el nombre filtraba un proveedor que ya no se usa (rompía el DIP).</summary>
    public interface IChatCompletionService
    {
        Task<ChatCompletionResult> GenerarAsync(ChatPrompt prompt, CancellationToken ct = default);
    }
}
