using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

        // ---- Phase 2: Chat & Model Service Registrations ----

        // ChatService manages chat conversations backed by FileConversationManager
        services.AddSingleton<IChatService, Services.ChatService>();

        // ModelService coordinates model discovery, metadata extraction, and management
        services.AddSingleton<IModelService, Services.ModelService>();

        // ModelMetadataService provides model metadata operations (extraction, validation, storage)
        services.AddSingleton<IModelMetadataService, Services.ModelMetadataService>();

        // TokenEstimator provides standardized token counting using character-based estimation
        services.AddSingleton<ITokenEstimator, Services.TokenEstimator>();

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

        // ---- Phase 5.5: Context Window Budgeting ----

        // ContextWindowBudgeter manages per-chat token budget and auto-evicts segments when exceeded
        services.AddSingleton<IContextWindowBudgeter, Services.ContextWindowBudgeter>();

        // ---- Phase 5.8: Context Inheritance System ----

        // TaskContextInheritor propagates relevant context from parent to child tasks with budget-aware filtering
        services.AddSingleton<ITaskContextInheritor, Services.TaskContextInheritor>();

        // ---- Phase 5.9: Fast Re-Injection Pipeline ----

        // TaskContextReinjectionService restores full context for paused/abandoned agent tasks in <100ms
        services.AddSingleton<ITaskContextReinjectionService, Services.TaskContextReinjectionService>();

        // ---- Phase 5.10: Context Pruning on Completion ----

        // TaskContextPruner manages archive/compress-and-archive/discard strategies for task completion
        services.AddSingleton<ITaskContextPruner, Services.TaskContextPruner>();

        // ---- Phase 3.4: HTTPS Certificate Generation ----

        // SelfSignedCertificateGenerator generates cross-platform self-signed certs for local HTTPS development
        services.AddSingleton<ISelfSignedCertificateService, Services.SelfSignedCertificateGenerator>();

        // ---- Phase 7: Agent Harness ----

        // FileOperationsService handles file read/write operations within the agent sandbox
        services.AddSingleton<IFileOperationsService, Services.FileOperationsService>();

        // CommandExecutionService executes shell commands in a sandboxed environment (cross-platform)
        services.AddSingleton<ICommandExecutionService, Services.CommandExecutionService>();

        // AgentTaskProgressTracker tracks agentic task progress through stages (NotStarted → InProgress → Reviewing → Completed/Failed)
        services.AddSingleton<ITaskProgressTracker, Services.AgentTaskProgressTracker>();

        // ---- Phase 7: Built-in Tool Registration ----

        // FileRead tool for reading file contents within the agent sandbox
        services.AddSingleton<ITool, Services.FileReadTool>();

        // FileWrite tool for writing/creating files within the agent sandbox
        services.AddSingleton<ITool, Services.FileWriteTool>();

        // FilePatch tool for safely patching files within the agent sandbox
        services.AddSingleton<ITool, Services.FilePatchTool>();

        // CommandExecute tool for running shell commands in a sandboxed environment
        services.AddSingleton<ITool, Services.CommandExecuteTool>();

        // SearchFiles tool for regex search across project files
        services.AddSingleton<ITool, Services.SearchFilesTool>();

        // ProjectExplorer tool for listing directory contents recursively
        services.AddSingleton<ITool, Services.ProjectExplorerTool>();

        // GitDiff tool for showing differences between git refs
        services.AddSingleton<ITool, Services.GitDiffTool>();

        // GitHistory tool for listing recent commits
        services.AddSingleton<ITool, Services.GitHistoryTool>();

        // GitBlame tool for line-by-line attribution
        services.AddSingleton<ITool, Services.GitBlameTool>();

        // GitBranches tool for listing branches, tags, and remotes
        services.AddSingleton<ITool, Services.GitBranchesTool>();

        // CodeDefinitionExtractor tool for extracting class/function/method definitions from a project
        services.AddSingleton<ITool, Services.CodeDefinitionExtractorTool>();

        // ---- Phase 2: Model Management — IModelManager + Loader Registration ----

        // ModelManager coordinates concurrent multi-model loading across all engine types with eviction policy.
        services.AddSingleton<IModelManager, Services.ModelManager>();

        // GgufChatCompletionLoader adapts LlamaCppChatCompletionService to IModelLoader for text generation models.
        // Registered as a factory so it gets the existing chat service from DI rather than creating its own instance.
        services.AddScoped<IModelLoader>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.GgufChatCompletionLoader>>();
            var chatService = resolver.GetService<Services.LlamaCppChatCompletionService>();
            if (chatService == null)
                throw new InvalidOperationException("LlamaCppChatCompletionService not found in DI container — required for text generation model loading.");

            return new Services.GgufChatCompletionLoader(logger ?? NullLogger<Services.GgufChatCompletionLoader>.Instance, chatService);
        });

        // ---- Phase 7: Agent Harness — Core Agent Registration ----

        // IAgent interface for managing the lifecycle of an agentic task with plan/act cycle.
        // Registered as a factory because it needs both ILogger<Agent> and ITaskProgressTracker,
        // but also needs to discover available tools at runtime from DI (MCP + built-in).
        services.AddScoped<IAgent>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.Agent>>();
            var progressTracker = resolver.GetService<ITaskProgressTracker>();
            if (progressTracker == null)
                throw new InvalidOperationException("ITaskProgressTracker not found in DI container — required for Agent lifecycle tracking.");

            return new Services.Agent(logger ?? NullLogger<Services.Agent>.Instance, progressTracker);
        });

        // ---- Phase 8: Plugin & MCP System ----

        // PluginRegistry manages plugin discovery, installation, and lifecycle from local/appdata directory
        var pluginDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),  // Windows: %APPDATA%\OpenLMStudio\plugins\
            "OpenLMStudio",
            "plugins");
        services.AddSingleton<Domain.Interfaces.IPluginRegistry>(resolver =>
            new Services.PluginRegistry(resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.PluginRegistry>>(), pluginDir));

        // ---- Phase 9: Resilience & Security System ----

        // SandboxService provides cross-platform process isolation (Job Objects on Windows, cgroups v2 on Linux/macOS)
        services.AddSingleton<Domain.Interfaces.ISandboxService, Services.SandboxService>();

        // ---- Phase 7: Agent Harness — Tool Registry ----

        // ToolRegistry manages tool discovery and instantiation for agent execution
        services.AddSingleton<IToolRegistry>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.ToolRegistry>>();
            var serviceProvider = resolver;
            return new Services.ToolRegistry(logger, serviceProvider);
        });

        // ActiveProjectWatcher provides real-time project filesystem monitoring for the agent
        services.AddSingleton<Services.ActiveProjectWatcher>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.ActiveProjectWatcher>>();
            var appData = resolver.GetService<AppDataDirectoryResolver>();
            var watchPath = appData?.TaskDirectory ?? Directory.GetCurrentDirectory();
            return new Services.ActiveProjectWatcher(logger, watchPath);
        });

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