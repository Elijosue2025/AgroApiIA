using AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;
using AgroGuia.Infraestructura.ServicioExterno.Embeddings;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Ollama;
using AgroGuia.Infraestructura.ServicioExterno.OpenAI;
using AgroGuia.Infraestructura.ServicioExterno.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Ollama;
using OllamaSharp.Models;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.DependencyInjection;

/// <summary>
/// MEJORAS: configuración tipada para todo, HttpClient por IHttpClientFactory, lifetimes correctos
/// (stateless/caché = singleton; lo que toca repositorios = scoped) y registro de cada puerto.
/// </summary>
public static class ServicioExternoRegistration
{
    public static IServiceCollection AddServicioExterno(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
        services.Configure<RagOptions>(configuration.GetSection("Rag"));
        services.Configure<ChunkingOptions>(configuration.GetSection("Chunking"));
        services.Configure<DocumentosConfig>(configuration.GetSection("Documentos"));

        // ---- Ollama: embeddings (HttpClient tipado) ----
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        });

        // ---- Ollama: chat ----
        services.AddHttpClient("ollama-chat", (sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        });
        services.AddSingleton<IOllamaApiClient>(sp => new OllamaApiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama-chat"),
            sp.GetRequiredService<IOptions<OllamaOptions>>().Value.ChatModel));

        // ---- Proveedor del chat: "Llm:Provider" = "Ollama" (por defecto) | "OpenAI" ----
        // Los embeddings siguen siendo siempre locales (nomic-embed-text); solo cambia quién redacta la respuesta.
        IServiceCollection servicesWithOpenAIConfig = services.Configure<OpenAIConfig>(configuration.GetSection("OpenAI"));
        if (string.Equals(configuration["Llm:Provider"], "OpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IChatCompletionService, OpenAIChatCompletionService>();
        else
            services.AddScoped<IChatCompletionService, OllamaChatService>();

        // ---- Ingesta ----
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<ITextCleaner, TextCleaner>();
        services.AddSingleton<IChunker, ParagraphChunker>();
        services.AddSingleton<ICatalogoAgronomico, CatalogoAgronomico>();
        services.AddScoped<DocumentLoaderService>();
        //services.AddScoped<IOpenAIService, OllamaChatService>();

        // ---- RAG ----
        services.AddSingleton<SimilarityService>();
        services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();
        services.AddSingleton<IVectorSearchService, VectorSearchService>();
        services.AddSingleton<PromptBuilder>();
        services.AddScoped<ContextRetriever>();
        services.AddScoped<IRAGEngine, RagEngine>();

        // ---- Visión por computador (OpenCV): reconocimiento de plagas y enfermedades ----
        services.AddVisionOpenCv(configuration);

        return services;
    }
}