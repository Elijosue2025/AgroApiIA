using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace AgroGuia.Infraestructura.ServicioExterno.Ollama
{
    public class OllamaChatService : IOpenAIService
    {
        private readonly OllamaApiClient _client;
        private readonly string _modelName = "llama3.2:3b"; // Cambia a "qwen2.5:7b" si tienes más RAM

        public OllamaChatService()
        {
            _client = new OllamaApiClient(new Uri("http://localhost:11434"));
            _client.SelectedModel = _modelName;
        }
        private string GenerarRespuestaFallback(string pregunta, List<string> chunks)
        {
            if (chunks == null || !chunks.Any())
                return "Lo siento, no tengo información suficiente en los manuales para responder esta consulta.";

            return $"**Información encontrada en los manuales técnicos:**\n\n" +
                   string.Join("\n\n", chunks.Take(4));
        }
        public async Task<OpenAIChatResponse> ObtenerRespuestaConRAGAsync(
            string mensajeUsuario, List<string> chunksRelevantes)
        {
            try
            {
                var chat = new Chat(_client);

                string systemPrompt = @"
                Eres AgroGuia IA, un ingeniero agrónomo experto de la Sierra Norte del Ecuador (Carchi).
                Responde de forma clara, práctica y útil para agricultores.
                Sé honesto si no tienes información suficiente en el contexto.";

                await foreach (var _ in chat.SendAsAsync(ChatRole.System, systemPrompt)) { }

                if (chunksRelevantes?.Any() == true)
                {
                    string contexto = string.Join("\n\n────────────────────\n\n",
                        chunksRelevantes.Select(c =>
                            c.Length > 500 ? c.Substring(0, 500) + "..." : c));
                    await foreach (var _ in chat.SendAsAsync(ChatRole.System, $"CONTEXTO DE LOS MANUALES TÉCNICOS:\n{contexto}")) { }
                }

                string respuesta = "";
                await foreach (var fragmento in chat.SendAsAsync(ChatRole.User, mensajeUsuario))
                {
                    respuesta += fragmento;
                }

                return new OpenAIChatResponse
                {
                    Exito = true,
                    Respuesta = respuesta.Trim(),
                    TokensTotales = 0
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ollama Error] {ex.Message}");
                return new OpenAIChatResponse
                {
                    Exito = true,
                    Respuesta = "Lo siento, el asistente local no está disponible en este momento."
                };
            }
        }
    }
}