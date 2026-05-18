using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure;

/// <summary>
/// Service collection extensions for the Infrastructure layer.
/// Registers all infrastructure implementation types as their interface implementations.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure-layer services with the dependency injection container.
    /// Should be called after AddApplicationTypes to ensure interfaces are available.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // ServerService manages local inference server lifecycle (ASP.NET Core Kestrel)
        services.AddSingleton<IServerService, Services.ServerService>();

        // ConversationManager handles conversation CRUD and persistence
        services.AddSingleton<IConversationManager, Services.FileConversationManager>();

        // DeviceMonitor tracks hardware capabilities (GPU, CPU, memory)
        services.AddSingleton<IDeviceMonitor, Services.WindowsDeviceMonitor>();

        // MCP Client for Model Context Protocol communication (stub - pending implementation)
        services.AddSingleton<IMcpClient, Services.McpStdioClient>();

        // ChatCompletionService handles LLM inference requests (uses the actual implementation)
        services.AddSingleton<IChatCompletionService, Services.LlamaCppChatCompletionService>();

        // GgufParser extracts metadata from GGUF model file headers (merged implementation with little-endian byte order)
        services.AddSingleton<GgufParser>();

        // ModelRepository manages GGUF model discovery and metadata storage
        services.AddSingleton<IModelRepository, Services.JsonModelRepository>();

        // DownloadManager handles model downloads from HuggingFace and other sources
        services.AddSingleton<IDownloadManager, Services.DownloadManager>();

        // ---- Multi-modal Pipeline Services (ONNX Runtime-based) ----

        // DiffusionPipelineService for image generation using ONNX Runtime + safetensors models
        services.AddSingleton<IDiffusionPipelineService, Services.DiffusionPipelineService>();

        // VAEPipelineService for latent space encoding/decoding using ONNX Runtime + VAE models
        services.AddSingleton<IVAEPipelineService, Services.VAEPipelineService>();

        // LoraAdapterManager for applying LoRA adapters during image generation
        services.AddSingleton<ILoraAdapterManager, Services.LoraAdapterManager>();

        // EmbeddingPipelineService for text/image embedding vectors using ONNX Runtime + safetensors models
        services.AddSingleton<IEmbeddingPipelineService, Services.EmbeddingPipelineService>();

        // AppDataDirectoryResolver resolves platform-specific appdata paths (Windows/macOS/Linux)
        services.AddSingleton<AppDataDirectoryResolver>();

        // SqliteDatabaseFactory provides cross-platform SQLite database connections
        services.AddSingleton<SqliteDatabaseFactory>();

        // TaskContextStore manages agentic task context snapshots (SQLite-backed)
        services.AddSingleton<ITaskContextStore, Services.SqliteTaskContextStore>();

        // ---- Context Management Services ----

        // ChatContextManager manages per-chat conversation context with compression/injection capabilities
        services.AddSingleton<IChatContextManager, Services.ChatContextManager>();

        // ContextCompressor provides multiple compression strategies (light/medium/aggressive)
        services.AddSingleton<IContextCompressor, Services.ConversationContextCompressor>();

        // ContextRelevanceEngine scores segment relevance based on recency, semantic content, and entity matching
        services.AddSingleton<IContextRelevanceEngine, Services.ContextRelevanceEngine>();

        // ContextManipulator handles user-driven context segment control (pin, suppress, custom injection)
        services.AddSingleton<IContextManipulator, Services.ContextManipulator>();

        return services;
    }

    /// <summary>
    /// Convenience method to register all OpenLMStudio services (Application + Infrastructure).
    /// </summary>
    public static IServiceCollection AddOpenLMStudioServices(this IServiceCollection services)
    {
        services.AddInfrastructureServices();

        return services;
    }
}