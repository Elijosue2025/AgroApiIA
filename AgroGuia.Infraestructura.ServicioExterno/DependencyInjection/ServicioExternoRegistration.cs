
using AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;
using AgroGuia.Infraestructura.ServicioExterno.Embeddings;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Ollama;
using AgroGuia.Infraestructura.ServicioExterno.OpenAI;
using AgroGuia.Infraestructura.ServicioExterno.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgroGuia.Infraestructura.ServicioExterno.DependencyInjection
{
    public static class ServicioExternoRegistration
    {
        public static IServiceCollection AddServicioExterno(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<OpenAIConfig>(configuration.GetSection("OpenAI"));
            services.Configure<DocumentosConfig>(configuration.GetSection("Documentos"));

            // ==================== CACHÉ ====================
            services.AddMemoryCache(options =>
            {
                // Límite de 50MB para el caché de embeddings
                options.SizeLimit = 50_000;
            });

            // ==================== PRINCIPAL: OLLAMA ====================
            services.AddScoped<IOpenAIService, OllamaChatService>();

            // ==================== EMBEDDINGS (Ollama local) ====================
            services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();

            // ==================== RAG ====================
            services.AddScoped<IRAGEngine, RagEngine>();
            services.AddScoped<ContextRetriever>();
            services.AddScoped<PromptBuilder>();
            services.AddScoped<SimilarityService>();

            // ==================== DOCUMENT LOADER ====================
            services.AddScoped<DocumentLoaderService>();

            return services;
        }
    }
}