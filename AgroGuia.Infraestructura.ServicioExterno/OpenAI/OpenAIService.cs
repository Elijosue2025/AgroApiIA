using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Models;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace AgroGuia.Infraestructura.ServicioExterno.OpenAI
{
    public class OpenAIService : IOpenAIService
    {
        private readonly OpenAIClient _client;
        private readonly OpenAIConfig _config;

        public OpenAIService(IOptions<OpenAIConfig> options)
        {
            _config = options.Value;

            if (string.IsNullOrWhiteSpace(_config.ApiKey))
                throw new Exception("OpenAI ApiKey no configurada.");

            _client = new OpenAIClient(_config.ApiKey);
        }

        public async Task<OpenAIChatResponse> ObtenerRespuestaConRAGAsync(
            string mensajeUsuario,
            List<string> chunksRelevantes)
        {
            try
            {
                var messages = new List<ChatMessage>();

                // Prompt del sistema
                string promptSistema = @"
Eres AgroGuia IA, un asistente agrícola experto en cultivos de la Sierra Norte del Ecuador (Carchi).
Responde de forma clara, práctica y amigable al agricultor.
Usa solo la información del contexto proporcionado.";

                messages.Add(new SystemChatMessage(promptSistema));

                // Inyectar contexto RAG
                if (chunksRelevantes?.Any() == true)
                {
                    string contexto = string.Join("\n\n-----------------\n\n", chunksRelevantes);
                    messages.Add(new SystemChatMessage($"CONTEXTO TÉCNICO OFICIAL:\n{contexto}"));
                }

                messages.Add(new UserChatMessage(mensajeUsuario));

                var options = new ChatCompletionOptions
                {
                    Temperature = _config.Temperature,
                    MaxOutputTokenCount = _config.MaxTokens
                };

                var chatClient = _client.GetChatClient(_config.ModeloChat);
                var completion = await chatClient.CompleteChatAsync(messages, options);

                return new OpenAIChatResponse
                {
                    Exito = true,
                    Respuesta = completion.Value.Content[0].Text.Trim(),
                    TokensEntrada = completion.Value.Usage?.InputTokenCount ?? 0,
                    TokensSalida = completion.Value.Usage?.OutputTokenCount ?? 0,
                    TokensTotales = completion.Value.Usage?.TotalTokenCount ?? 0
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenAI Error] {ex.Message}");

                // ==================== FALLBACK ====================
                if (ex.Message.Contains("insufficient_quota") ||
                    ex.Message.Contains("429") ||
                    ex.Message.Contains("quota"))
                {
                    return new OpenAIChatResponse
                    {
                        Exito = true, // Para que no rompa el flujo
                        Respuesta = GenerarRespuestaFallback(mensajeUsuario, chunksRelevantes),
                        TokensTotales = 0,
                        ErrorMensaje = "Se usó modo fallback por cuota excedida"
                    };
                }

                // Otros errores
                return new OpenAIChatResponse
                {
                    Exito = false,
                    Respuesta = "Lo siento, ocurrió un error al procesar tu consulta.",
                    ErrorMensaje = ex.Message
                };
            }
        }

        // Fallback: Respuesta basada solo en los chunks encontrados
        private string GenerarRespuestaFallback(string pregunta, List<string> chunks)
        {
            if (chunks == null || !chunks.Any())
            {
                return "Lo siento, no tengo información suficiente sobre esta consulta en este momento. " +
                       "Por favor, intenta más tarde o contacta a un técnico agrónomo.";
            }

            return $"**Respuesta basada en guías técnicas disponibles:**\n\n" +
                   string.Join("\n\n", chunks.Take(3)) +
                   $"\n\n¿Quieres que te explique mejor algún punto sobre '{pregunta}'?";
        }
    }
}