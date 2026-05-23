using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Services;
using OpenLMStudio.Infrastructure.Services;
using OpenLMStudio.Infrastructure.Services.QEMU;

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

        // DiffusionModelFamilyService manages configuration for SD 1.x, SDXL, SD 3, Flux model families
        services.AddSingleton<Application.Interfaces.IDiffusionModelFamilyService, Services.DiffusionModelFamilyService>();

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

        // MemoryManager tracks GPU VRAM and CPU memory allocations across loaded models, manages eviction by recency/frequency
        services.AddSingleton<OpenLMStudio.Application.Interfaces.IMemoryManager, MemoryManager>();

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

        // TaskService manages agentic tasks — creation, execution via IAgent, dependency tracking, and persistence
        services.AddSingleton<ITaskService, Services.TaskService>();

        // SqliteTaskRepository provides SQLite-backed CRUD operations for agentic tasks with priority scheduling
        services.AddSingleton<ITaskRepository>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.SqliteTaskRepository>>();
            var appData = resolver.GetService<AppDataDirectoryResolver>();
            var dbPath = Path.Combine(appData?.TaskDirectory ?? Directory.GetCurrentDirectory(), "tasks.db");
            return new Services.SqliteTaskRepository(logger, $"Data Source={dbPath}");
        });

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

        // ---- Phase 7: Pingu Tools (System AI Mascot) ----

        // PinguTabSwitch tool for switching tabs in the UI
        services.AddSingleton<ITool, Services.PinguTabSwitchTool>();

        // PinguPanelToggle tool for toggling UI panels
        services.AddSingleton<ITool, Services.PinguPanelToggleTool>();

        // PinguModelLoad tool for loading/unloading models
        services.AddSingleton<ITool, Services.PinguModelLoadTool>();

        // PinguGame tool for launching/stopping built-in games
        services.AddSingleton<ITool, Services.PinguGameTool>();

        // PinguWandering tool for autonomous exploratory behavior
        services.AddSingleton<ITool, Services.PinguWanderingTool>();

        // PinguModel tool for model management (load/unload/switch/list/status)
        services.AddSingleton<ITool>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.PinguModelTool>>();
            var modelRepo = resolver.GetService<IModelRepository>();
            var modelManager = resolver.GetService<Services.ModelManager>();
            return new Services.PinguModelTool(logger, modelRepo, modelManager);
        });

        // PinguGameIntegration tool for launching/stopping built-in games
        services.AddSingleton<ITool, Services.PinguGameIntegrationTool>();

        // IGamesPanel — implemented by the Avalonia GamesPanel, registered in Desktop layer

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

        // DiffusionModelLoader adapts DiffusionPipelineService to IModelLoader for image generation models.
        services.AddScoped<IModelLoader>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.DiffusionModelLoader>>();
            var pipeline = resolver.GetService<Services.DiffusionPipelineService>();
            var modelRepo = resolver.GetService<IModelRepository>();
            if (pipeline == null || modelRepo == null)
                throw new InvalidOperationException("DiffusionPipelineService and IModelRepository not found in DI container — required for image generation model loading.");

            return new Services.DiffusionModelLoader(logger ?? NullLogger<Services.DiffusionModelLoader>.Instance, pipeline, modelRepo);
        });

        // ---- Phase 7: Agent Harness — Core Agent Registration ----

        // IAgent interface for managing the lifecycle of an agentic task with plan/act cycle.
        // Registered as a factory because it needs both ILogger<Agent> and ITaskProgressTracker,
        // plus optional IContextCompressor for loop detection and degraded action generation.
        services.AddSingleton<IAgent>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.Agent>>();
            var progressTracker = resolver.GetService<ITaskProgressTracker>();
            var contextCompressor = resolver.GetService<IContextCompressor>();
            var toolRegistry = resolver.GetService<IToolRegistry>();
            var chatService = resolver.GetService<IChatCompletionService>();
            var contextManager = resolver.GetService<IChatContextManager>();

            if (progressTracker == null)
                throw new InvalidOperationException("ITaskProgressTracker not found in DI container — required for Agent lifecycle tracking.");

            return new Services.Agent(
                logger ?? NullLogger<Services.Agent>.Instance,
                progressTracker,
                toolRegistry,
                chatService,
                contextManager,
                contextCompressor);
        });

        // ---- Phase 8: Plugin & MCP System ----

        // PluginRegistry manages plugin discovery, installation, and lifecycle from local/appdata directory
        var pluginDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),  // Windows: %APPDATA%\OpenLMStudio\plugins\
            "OpenLMStudio",
            "plugins");
        services.AddSingleton<Domain.Interfaces.IPluginRegistry>(resolver =>
            new Services.PluginRegistry(resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.PluginRegistry>>(), pluginDir));

        // PluginSecurityValidator verifies plugin provenance (hash) and manifest integrity
        services.AddSingleton<IPluginSecurityValidator, Services.PluginSecurityValidator>();

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
        services.AddSingleton<IActiveProjectWatcher>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.ActiveProjectWatcher>>();
            var projectExplorer = resolver.GetService<IProjectExplorer>();
            var appData = resolver.GetService<AppDataDirectoryResolver>();
            var watchPath = appData?.TaskDirectory ?? Directory.GetCurrentDirectory();
            return new Services.ActiveProjectWatcher(logger, watchPath, projectExplorer ?? throw new InvalidOperationException("IProjectExplorer not found in DI container."));
        });

        // IImagePostProcessingService for upscaling, hires.fix, ControlNet, and IP-Adapter
        services.AddSingleton<IImagePostProcessingService, Services.ImagePostProcessingService>();

        // IFilePreviewService provides file content previews for the agent sandbox
        services.AddSingleton<IFilePreviewService, Services.FilePreviewService>();

        // DigitalSignatureVerifier verifies RSA signatures on model files
        services.AddSingleton<IDigitalSignatureVerifier, Services.DigitalSignatureVerifier>();

        // ---- Phase 7: Agent Harness — Communication Protocol ----

        // AgentCommunicationProtocol handles plan/act phase transitions and user approval gating
        services.AddSingleton<AgentCommunicationProtocol>();

        // AgentSystemPromptGenerator dynamically assembles system prompts based on available tools and context
        services.AddSingleton<AgentSystemPromptGenerator>();

        // AgentProtocolService manages plan/act phase transitions with user approval gating
        services.AddSingleton<IAgentProtocolService, Services.AgentProtocolService>();

        // AgentSessionPersister persists and restores agent session state to disk for crash recovery
        services.AddSingleton<AgentSessionPersister>(resolver =>
        {
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<Services.AgentSessionPersister>>();
            var appData = resolver.GetService<AppDataDirectoryResolver>();
            var sessionsDir = appData?.TaskDirectory ?? Directory.GetCurrentDirectory();
            return new Services.AgentSessionPersister(logger, sessionsDir);
        });

        // ---- Phase 7: Task Completion Detection ----

        // TaskCompletionDetector detects whether an agent task has been completed based on tool results
        services.AddSingleton<Application.Interfaces.ITaskCompletionDetector, Services.TaskCompletionDetector>();

        // AgentProgressSummaryService generates human-readable progress summaries and completion reports
        services.AddSingleton<Application.Interfaces.IAgentProgressSummaryService, Services.AgentProgressSummaryService>();

        // TaskValidationService provides AI-powered task completion verification using System AI
        services.AddSingleton<Application.Interfaces.ITaskValidationService, Services.TaskValidationService>();

        // TaskSchedulerService manages the shared task tree with access control (Pingu=admin, User=write, AIs=read-only)
        services.AddSingleton<Application.Interfaces.ITaskScheduler, Services.TaskSchedulerService>();

        // ---- Phase 10.5: Observability & Diagnostics ----

        // ActivityTracer traces agent tool calls with timing and resource consumption
        services.AddSingleton<Application.Interfaces.IActivityTracer, Tracing.ActivityTracer>();

        // ModelLifecycleTracer tracks per-model load/unload timing and VRAM allocation
        services.AddSingleton<Tracing.ModelLifecycleTracer>();

        // WindowSettingsService persists and restores main window state (position, size, active tab)
        services.AddSingleton<IWindowSettings, Services.WindowSettingsService>();

        // ---- Pingu (System AI Mascot) ----

        // PinguStore manages reactive Pingu state machine (mood, awakening, blink, animation)
        services.AddSingleton<IPinguStore, PinguStore>();

        // PinguPromptGenerator generates context-aware system prompts for Pingu based on assigned tasks and current state
        services.AddSingleton<IPinguPromptGenerator, PinguPromptGenerator>();

        // SystemAIClient provides llama.cpp inference via HTTP POST streaming
        services.AddSingleton<ISystemAIClient, SystemAIClient>();

        // QEMUProcessManager manages VM lifecycle and QMP protocol communication
        services.AddSingleton<IQEMUProcessManager, QEMUProcessManager>();

        // ArchPromptService provides architecture-specific system prompts for cross-compilation
        services.AddSingleton<IArchPromptService, ArchPromptService>();

        // VMStore provides reactive VM instance state storage
        services.AddSingleton<IVMStore, VMStore>();

        // EngineLogger provides structured logging with disk rotation for engine stdout/stderr
        services.AddSingleton<IEngineLogger, EngineLogger>();

        // ContextCompressionService compresses conversation history using System AI
        services.AddSingleton<IContextCompressionService, ContextCompressionService>();

        // HardwareDetector detects GPU/RAM for backend recommendation
        services.AddSingleton<HardwareDetector>();

        // ToolchainRegistry downloads and caches architecture-specific compiler toolchains
        services.AddSingleton<IToolchainRegistry, ToolchainRegistry>();

        // PinguAutomation provides action animations and drag-to-pause VM management
        services.AddSingleton<IPinguAutomation, PinguAutomation>();

        // ResourceManager monitors CPU/memory with VM-aware allocation
        services.AddSingleton<IResourceManager, ResourceManager>();

        // SystemAICoordinator orchestrates System AI with QEMU VMs for cross-architecture workflows
        services.AddSingleton<ISystemAICoordinator, SystemAICoordinator>();

        // ConversationEncryptionService provides AES-256-GCM encryption for conversation data at rest
        services.AddSingleton<ConversationEncryptionService>();

        // EngineBinaryDownloader downloads and caches llama.cpp engine binaries from GitHub releases
        services.AddSingleton<EngineBinaryDownloader>();

        // EngineConfigService persists and loads engine configuration to/from disk
        services.AddSingleton<IEngineConfigService, EngineConfigService>();

        // AppUpdateChecker checks for application updates via GitHub Releases API
        services.AddSingleton<IAppUpdateChecker, AppUpdateChecker>();

        // OnboardingService tracks first-run onboarding completion state
        services.AddSingleton<IOnboardingService, OnboardingService>();

        // UpdateManager provides application-level update checking via GitHub Releases API.
        services.AddSingleton<IUpdateManager>(resolver =>
            new UpdateManager(
                resolver.GetService<Microsoft.Extensions.Logging.ILogger<UpdateManager>>() ?? NullLogger<UpdateManager>.Instance,
                resolver.GetService<AppDataDirectoryResolver>() ?? new AppDataDirectoryResolver(),
                resolver.GetService<Domain.Interfaces.IPluginRegistry>()));

        // InteractiveHelpService provides in-app interactive help with topic search and keyboard navigation
        services.AddSingleton<IInteractiveHelpService, InteractiveHelpService>();

        // UX Services: Keyboard shortcuts, accessibility, and onboarding
        services.AddSingleton<IKeyboardShortcuts, KeyboardShortcutsService>();
        services.AddSingleton<IAccessibilityService, AccessibilityService>();
        services.AddSingleton<IKeyboardNavigationService, KeyboardNavigationService>();

        // Context-aware suggestion service: generates next-action suggestions based on conversation/task/project state
        services.AddSingleton<IContextAwareSuggestionService, ContextAwareSuggestionService>();

        // Markdown renderer: converts Markdown to HTML with code block syntax highlighting (C#, Python, JS, etc.)
        services.AddSingleton<IMarkdownRenderer, Services.SyntaxHighlightingMarkdownRenderer>();

        // PerformanceBenchmarkService benchmarks model load time, chat throughput, and image generation latency
        services.AddSingleton<Services.PerformanceBenchmarkService>();

        // ServerLoadTestService performs load testing of server endpoints under concurrent requests
        services.AddSingleton<Services.ServerLoadTestService>();

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