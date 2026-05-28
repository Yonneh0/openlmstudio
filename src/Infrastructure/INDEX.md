## src/Infrastructure/Services/ActivityTracer.cs - ~250 lines - Agent Activity Tracer (consolidated)
   - IActivityTracer implementation for tracing agent tool calls with timing and resource consumption. Uses ConcurrentDictionary for per-task trace storage. Merged StructuredLoggerExtensions with typed log events: ModelLoadStarted/Completed/Failed, ModelUnloadStarted, ContextCompressed, AgentTaskStarted/Completed, AgentToolCall, ServerStarted/Stopped.
## src/Infrastructure/Services/ArchPromptService.cs - 119 lines - Architecture-Specific QEMU System Prompts
  - IArchPromptService implementation with 14 architecture entries (X86_64, AArch64, RISC_V64, AVR, MIPS, etc.) and cross-compile environment variables.
## src/Infrastructure/Services/DependencyInjection.cs - 509 lines - Infrastructure Layer Dependency Injection
  - Static class with AddInfrastructureServices() and AddOpenLMStudioServices(). Registers 80+ services including IServerService, IConversationManager, IDeviceMonitor, IModelService, IAgent, IToolRegistry, IPluginRegistry, ITaskScheduler, ITaskValidationService, IPinguStore, IQEMUProcessManager, ISystemAICoordinator.
## src/Infrastructure/OpenLMStudio.Infrastructure.csproj - 33 lines - Infrastructure Project File
  - .NET 8 SDK-style project with references to Domain and Application layers. Packages: Markdig.SyntaxHighlighting, Microsoft.Extensions.DependencyInjection, Microsoft.Data.Sqlite.Core, SQLitePCLRaw, SkiaSharp, System.Management.
## src/Infrastructure/Services/PinguPromptGenerator.cs - 382 lines - Pingu System Prompt Generator
  - IPinguPromptGenerator implementation with GenerateFullPrompt, GenerateCompressedPrompt, GenerateForTaskType. Private prompt builders for TaskOrchestrator, UIControl, ModelManagement, GamePlay, Wandering, UserAssistant.
## src/Infrastructure/Services/PinguStore.cs - 236 lines - Reactive Pingu State Store
  - IPinguStore implementation with PinguEvent bus. UpdateMoodAsync, ToggleMenuAsync, SetActivePanelAsync, SetAwakeAsync, StartAwakeningSequenceAsync, StartBlinkTimerAsync, CompleteTaskAsync, HandleTaskErrorAsync.
## src/Infrastructure/Services/QEMUProcessManager.cs - ~450 lines - QEMU Virtual Machine Process Manager
  - IQEMUProcessManager + IVMStore + IArchPromptService implementation with QMP protocol support. Contains _instances, _processes, _archBinaries, _stateTimer. Methods: CreateVMAsync, StartVMAsync, PauseVMAsync, ResumeVMAsync, StopVMAsync, DeleteVMAsync, ExecuteQMPCommandAsync, AddAsync, RemoveAsync, GetAsync, UpdateAsync.
## src/Infrastructure/Services/ResourceManager.cs - 122 lines - Resource Monitoring
  - IResourceManager implementation with VM-aware allocation. MonitorResourcesAsync, AdjustModelSettingsAsync, CompactPromptIfNeededAsync.
## src/Infrastructure/Services/AccessibilityService.cs - 94 lines - Accessibility Settings Manager
  - IAccessibilityService implementation with screen reader, high contrast, font size, and keyboard navigation settings.
## src/Infrastructure/Services/ActiveProjectTree.cs - 120 lines - Project Tree Node and File Preview Services
  - ProjectTreeNode, ProjectTreeChange, ProjectTreeChangeType, FilePreviewService. FilePreviewService.GetPreviewAsync and IsBinaryFile.
## src/Infrastructure/Services/ActiveProjectWatcher.cs - 272 lines - Real-Time Project Tree Change Monitor
  - IActiveProjectWatcher implementation with FileSystemWatcher. Debounces changes with 300ms interval. StartAsync, StopAsync, RefreshAsync, BuildTreeAsync.
## src/Infrastructure/Services/Agent.cs - 684 lines - Core Agent with Plan/Act Cycle
  - IAgent implementation with plan/act cycle and error recovery. Loop detection (50%+ same action), consecutive failure threshold (3), auto-commit for Write/Patch tools.
## src/Infrastructure/Services/AgentCommunicationProtocol.cs - 257 lines - Agent Communication Protocol
  - IAgentCommunicationProtocol implementation for plan/act phase transitions. AgentCommunicationPhase, AgentCommunicationMessage, PhaseTransitionEvent.
## src/Infrastructure/Services/AgentProgressSummaryService.cs - 94 lines - Agent Progress Summary Generator
  - IAgentProgressSummaryService implementation with GenerateSummary and GenerateReport methods.
## src/Infrastructure/Services/AgentProtocolService.cs - 182 lines - Agent Protocol Service
  - IAgentProtocolService implementation with phase transitions and approval gating. GeneratePlanAsync, ApprovePlanAsync, RejectPlanAsync, IsActionSafeAsync.
## src/Infrastructure/Services/AgentSessionPersister.cs - 201 lines - Agent Session Persistence to Disk
  - Persists and restores agent session state to/from disk. SaveSessionAsync, LoadSessionAsync, DeleteSessionAsync, ListSessions.
## src/Infrastructure/Services/AgentSystemPromptGenerator.cs - 254 lines - Dynamic Agent System Prompt Generator
  - Dynamically assembles agent system prompts. GeneratePlanningPrompt, GenerateActingPrompt, GenerateNextActionSuggestions, AutoDiscoverTools.
## src/Infrastructure/Services/AgentTaskProgressTracker.cs - 141 lines - Agent Task Progress Tracker
  - ITaskProgressTracker implementation with ConcurrentDictionary. UpdateStageAsync, ReportProgressAsync, RecordToolCallAsync, CheckIterationLimit.
## src/Infrastructure/Services/ApiKeyAuthMiddleware.cs - 108 lines - API Key Authentication Middleware
  - ApiKeyAuthMiddleware and ApiKeyAuthMiddlewareExtensions. Validates API key from X-Api-Key header or api_key query parameter.
## src/Infrastructure/Services/AppDataDirectoryResolver.cs - 259 lines - Cross-Platform AppData Directory Resolver
  - Resolves platform-specific OpenLMStudio appdata directories (Windows, macOS, Linux). SqliteDatabaseFactory with double-checked locking.
## src/Infrastructure/Services/AppUpdateChecker.cs - 156 lines - GitHub Releases Update Checker
  - IAppUpdateChecker implementation. Checks GitHub API for releases/latest, caches results for 1 hour.
## src/Infrastructure/Services/AvaloniaMarkdownRenderer.cs - 173 lines - Markdown-to-HTML Renderer for Avalonia
  - IMarkdownRenderer implementation with Render and ExtractPlainText. Supports code blocks, headers, lists, blockquotes, horizontal rules.
## src/Infrastructure/Services/BinaryRegistry.cs - 257 lines - Local Binary Storage with Version Tracking
  - Manages local binary storage with version tracking, branch management, and volatile binary support. RegisterDownloadedBinary, GetBestForBackend, LoadRegistry, SaveRegistry.
## src/Infrastructure/Services/ChatContextManager.cs - 534 lines - Per-Chat SQLite-Backed Context Manager
  - IChatContextManager implementation with SQLite-backed persistence. Four tables: ChatMessages, PinSegmentStates, SuppressSegmentStates, CustomInjections.
## src/Infrastructure/Services/ChatPersistenceService.cs - 463 lines - Consolidated Chat Conversation Persistence
  - IConversationManager implementation with file-based JSON persistence. ListChatsAsync, CreateChatAsync, LoadChatAsync, AddMessageAsync, SearchChatsAsync.
## src/Infrastructure/Services/ChatService.cs - 103 lines - Chat Service Wrapping IConversationManager
  - IChatService implementation with IConversationManager and ILogger. CreateChatAsync, GetChatAsync, ListChatsAsync, DeleteChatAsync, AddMessageAsync.
## src/Infrastructure/Services/CommandExecutionService.cs - 128 lines - Sandboxed Command Execution Service
  - ICommandExecutionService with cross-platform sandboxing (cgroups v2 on Linux/macOS, Job Objects on Windows). ExecuteAsync, CancelAsync, KillAsync.
## src/Infrastructure/Services/CompilationHelper.cs - 118 lines - Static Compilation Helper for Cross-Platform Builds
  - Static class with GetCompileScript, GetInstallCompilerCommand, CheckPrerequisites, GetOSName. Generates CMake commands for Windows/macOS/Linux.
## src/Infrastructure/Services/ContextAwareSuggestionService.cs - 249 lines - Context-Aware Action Suggestion Engine
  - IContextAwareSuggestionService implementation. Combines conversation, task, and project state suggestions. Sorts by confidence descending.
## src/Infrastructure/Services/ContextCompressionService.cs - 128 lines - Conversation History Compression Using System AI
  - IContextCompressionService implementation with System AI (1B CPU model). CompressConversationAsync, GenerateFullContext, GetCompressionStats.
## src/Infrastructure/Services/ContextManipulator.cs - 231 lines - User-Driven Context Segment Control
  - IContextManipulator implementation with 5 actions (Pin, Unpin, SuppressToggle, RemoveFromContext, AddCustomContext). Delegates to ChatContextManager.
## src/Infrastructure/Services/ContextRelevanceEngine.cs - 260 lines - Context Relevance Scoring Engine
  - IContextRelevanceEngine implementation with 30% recency, 40% semantic, 30% entity match scoring. ScoreSegmentsAsync, CalculateRelevanceThreshold, OrderByRelevance.
## src/Infrastructure/Services/ContextWindowBudgeter.cs - 309 lines - Token Budget Management with Auto-Eviction
  - IContextWindowBudgeter implementation with auto-eviction of lowest-relevance segments. GetOrCreateBudgetAsync, DeductFromBudgetAsync, TryAutoEvictLowestRelevanceSegmentsAsync.
## src/Infrastructure/Services/ConversationContextCompressor.cs - 638 lines - Multi-Strategy Context Compression
  - IContextCompressor implementation with 3 strategies: Light (key phrase extraction), Medium (summary generation), Aggressive (outline-level).
## src/Infrastructure/Services/ConversationEncryption.cs - 132 lines - AES-256-GCM Conversation Encryption
  - ConversationEncryptionService and ConversationEncryption static class. Encrypt/Decrypt with PBKDF2 key derivation (100K iterations, SHA256).
## src/Infrastructure/Services/ConversationManager.cs - 463 lines - File-Based Conversation Persistence
  - IConversationManager implementation with file-based JSON persistence. CreateChatAsync, LoadChatAsync, AddMessageAsync, SearchChatsAsync, ExportChatAsync.
## src/Infrastructure/Services/Crypto.cs - 168 lines - AES-256 Cryptographic Utility
  - Static class with GenerateKey, Encrypt, Decrypt, DeriveKey, ComputeSha256Hash, ConstantTimeEquals. Format: [Salt (32 bytes)][IV (16 bytes)][Encrypted Data].
## src/Infrastructure/Services/DeviceMonitor.cs - 367 lines - Windows Hardware Device Monitor
  - WindowsDeviceMonitor with WMI-based hardware detection (Win32_VideoController, Win32_Processor, Win32_OperatingSystem). 10s polling interval.
## src/Infrastructure/Services/DiffusionInferenceEngine.cs - 549 lines - ONNX Runtime Diffusion Inference Engine
  - CLIP text encoding → UNet denoising with CFG → VAE decoding pipeline. Supports SD1.5/SDXL/Flux pipelines. RunUnetDenoise, ApplyLoraDeltas, RunTextEncoder.
## src/Infrastructure/Services/DiffusionModelFamilyService.cs - 113 lines - Diffusion Model Family Configuration Manager
  - Manages diffusion model families (SD 1.5, SDXL, SD 3, Flux) with pipeline-specific configurations. RegisterFamily, RegisterDefaultFamilies.
## src/Infrastructure/Services/DiffusionModelLoader.cs - 155 lines - Image Generation Model Loader Adapter
  - IModelLoader for image generation models, wrapping DiffusionPipelineService. LoadModelAsync, UnloadModelAsync, GetModelMetadataAsync.
## src/Infrastructure/Services/DiffusionPipelineService.cs - 1378 lines - Full 3-Stage Diffusion Pipeline Service
  - IDiffusionPipelineService with ONNX Runtime-based image generation. GenerateImageAsync, GenerateInpaintingAsync, GenerateOutpaintingAsync, StreamProgressAsync.
## src/Infrastructure/Services/DigitalSignatureVerifier.cs - 85 lines - RSA Digital Signature Verifier
  - IDigitalSignatureVerifier implementation for model file integrity verification. VerifySignature (SHA256), VerifySignatureSha512, ComputeFileHash.
## src/Infrastructure/Services/DownloadManager.cs - 1024 lines - Model Download Manager with HuggingFace Support
  - IDownloadManager with HuggingFace Hub API, GgufParser, SafetensorParser. DownloadFromHuggingFaceAsync, DownloadSafetensorsModelAsync, MergeLoraAdapterAsync.
## src/Infrastructure/Services/EmbeddingPipelineService.cs - 540 lines - ONNX Runtime Embedding Pipeline Service
  - IEmbeddingPipelineService for text/image embedding generation. GenerateAsync, GenerateBatchAsync, LoadModelAsync, GetAvailableModelsAsync.
## src/Infrastructure/Services/EngineBinaryDownloader.cs - 714 lines - llama.cpp Engine Binary Downloader
  - Downloads llama.cpp engine binaries from ggml-org/llama.cpp GitHub releases. DownloadForBackendAsync, CheckForUpdateAsync, CleanupStaleTempFiles.
## src/Infrastructure/Services/EngineConfigService.cs - 129 lines - Engine Configuration Persistence
  - IEngineConfigService for llama.cpp server settings persistence. LoadAsync, SaveAsync, CreateDefaultConfig, MigrateConfig.
## src/Infrastructure/Services/EngineLogger.cs - 170 lines - Structured Engine Logging with Disk Rotation
  - IEngineLogger implementation for llama.cpp stdout/stderr structured logging. StartSessionAsync, StopSessionAsync, HandleEngineStdout, HandleEngineStderr.
## src/Infrastructure/Services/FileOperationsService.cs - 270 lines - File Operations Service for Agent Sandbox
  - IFileOperationsService and IDisposable. ReadFileAsync, WriteFileAsync, PatchFileAsync, SearchFilesAsync, ExploreProjectAsync.
## src/Infrastructure/Services/GgufChatCompletionLoader.cs - 110 lines - GGUF Chat Completion Loader Adapter
  - IModelLoader and IDisposable for text generation models, wrapping LlamaCppChatCompletionService. LoadModelAsync, UnloadModelAsync, GetModelMetadataAsync.
## src/Infrastructure/Services/GgufModelDownloader.cs - 276 lines - GGUF Model Downloader with HuggingFace Support
  - Downloads GGUF models from HuggingFace and discovers local GGUF files. InferQuantizationFromFilename (27 patterns), InferModelNameFromFilename.
## src/Infrastructure/Services/GgufParser.cs - 917 lines - GGUF Model File Parser
  - GGUF format specification (magic 0x46554747, v1-v3). TagMap (51 entries). ParseHeaderAsync, ParseAsync, ReadKeyValuePairAsync, ReadValueByTypeAsync.
## src/Infrastructure/Services/GitRepositoryService.cs - 551 lines - Git Repository Service with CLI Operations
  - IGitRepositoryService using git CLI. InitializeFromPathAsync, ListBranchesAsync, ListTagsAsync, ListCommitsAsync, GetDiffBetweenRefsAsync, GetBlameForFileAsync.
## src/Infrastructure/Services/HardwareDetector.cs - 134 lines - Hardware Detection for Backend Selection
  - Detects platform (Win32/Darwin/Linux), GPU (WMIC on Windows, SKInfo), RAM (wmic/sysctl/proc/meminfo). GetRecommendedBackend (darwin→metal, NVIDIA→cuda).
## src/Infrastructure/Services/ImagePostProcessingService.cs - 733 lines - Image Post-Processing with Upscaling and ControlNet
  - IImagePostProcessingService for image-to-image upscaling, HiRes.fix, ControlNet preprocessing (Canny/Depth/OpenPose), IP-Adapter face embeddings.
## src/Infrastructure/Services/InteractiveHelpService.cs - 118 lines - Interactive Help Service with Topic Search
  - IInteractiveHelpService with topic search and keyboard navigation. GetHelpAsync, SearchHelpAsync, ListTopicsAsync, AddTopicAsync.
## src/Infrastructure/Services/JsonModelRepository.cs - 762 lines - JSON Model Repository for GGUF + Safetensors
  - IModelRepository for GGUF and Safetensors model discovery/indexing. DiscoverModelsAsync, GetModelByIdAsync, SearchModelsAsync, IndexShardedModelAsync.
## src/Infrastructure/Services/KeyboardNavigationService.cs - 84 lines - Keyboard Navigation Service
  - IKeyboardNavigationService for accessibility-focused keyboard navigation. MoveFocusForward, MoveFocusBackward, FocusByName, GetFocusableElements.
## src/Infrastructure/Services/KeyboardShortcutsService.cs - 60 lines - Keyboard Shortcuts Service
  - IKeyboardShortcuts implementation with action callbacks. RegisterShortcut, UnregisterShortcut, GetShortcuts, RegisterHandler, TryInvoke.
## src/Infrastructure/Services/LlamaCppChatCompletionService.cs - 405 lines - Llama.cpp Chat Completion Service
  - IChatCompletionService and IDisposable for local inference with llama.cpp. GetCompletionAsync, GetStreamingCompletionAsync, LoadModelAsync, UnloadModelAsync.
## src/Infrastructure/Services/LlamaServerHelpParser.cs - 176 lines - Llama Server Help Parser
  - Parses llama-server --help output into structured HelpSetting records. GetSettingsAsync (executes llama-server --help, caches for 5 minutes).
## src/Infrastructure/Services/LogViewerService.cs - 183 lines - Real-Time Log Viewer with Filtering
  - Manages real-time log entries with filtering, searching, and colorization. GetFilteredLogs, AddLogEntry, SetMinLevel, SetSearchText.
## src/Infrastructure/Services/LoraAdapterManager.cs - 297 lines - LoRA Adapter Manager for Image Generation
  - ILoraAdapterManager for managing LoRA adapters in image generation pipelines. ApplyAdapterAsync, MergeAdapterAsync, GetAvailableAdaptersAsync.
## src/Infrastructure/Services/LoraWeightMerger.cs - 437 lines - LoRA Weight Merger for Image Generation
  - LoRA adapter weight merging with ONNX Runtime. LoadAdapterAsync, MergeAdapterAsync (applies W_base + alpha/rank * delta_W), SaveMergedSafetensors.
## src/Infrastructure/Services/MainAIManager.cs - 609 lines - MainAI Manager for Text Generation
  - Primary text generation engine lifecycle with multi-model support. LoadModelAsync, SwitchActiveModel, UnloadModel, StartServerAsync, BuildServerArgs.
## src/Infrastructure/Services/McpService.cs - 958 lines - Consolidated MCP Cluster
  - Transport layer: IMcpClient interface, McpToolDefinition, McpToolResult, McpStdioClient (stdio transport), McpSseClient (SSE transport), McpMessage, McpSseMessage. Tool layer: McpToolCaller (MCPToolCall), McpPromptAccessor (MCPGetPrompt), McpPromptListTool (MCPPromptsList), McpResourceAccessor (MCPResourceAccess).
## src/Infrastructure/Services/MemoryManager.cs - 165 lines - GPU VRAM and CPU Memory Tracker
  - IMemoryManager for tracking GPU VRAM and CPU memory allocations with eviction scoring. RegisterModel, RecordAccess, GetEvictionPriority, GetModelToEvict.
## src/Infrastructure/Services/ModelCacheCleanupService.cs - 166 lines - Model Cache Cleanup Service
  - IModelCacheCleanupService for scanning model directory and removing orphaned/unused files. CleanAsync (KeepAll/RemoveByLastUsed/EvictBySize/Aggressive).
## src/Infrastructure/Services/ModelLoadingFallbackService.cs - 200 lines - Model Loading Fallback Orchestrator
  - Fallback chain: GPU→CPU→degraded parameters. LoadWithFallbackAsync (catches OOM/GPU errors), GenerateLoadingAttemptSequence (4 strategies).
## src/Infrastructure/Services/ModelManager.cs - 331 lines - Model Manager with Concurrent Loading and VRAM Tracking
  - IModelManager and IDisposable for concurrent model loading/unloading with memory-aware eviction. LoadModelAsync, UnloadModelByIdAsync, EvictModelsToFreeVramAsync.
## src/Infrastructure/Services/ModelMetadataService.cs - 181 lines - Model Metadata Service
  - IModelMetadataService backed by GgufParser and JsonModelRepository. ExtractMetadataAsync, GetMetadataAsync, UpdateMetadataAsync, ListModelsAsync.
## src/Infrastructure/Services/ModelRecommendationService.cs - 208 lines - Model Recommendation Service
  - Smart default settings for models based on size, name, and architecture. GetRecommendation (from GgufModelInfo), GetRecommendationByName.
## src/Infrastructure/Services/ModelService.cs - 181 lines - Model Service
  - IModelService backed by GgufParser and JsonModelRepository. DiscoverModelsAsync, ExtractMetadataAsync, GetMetadataAsync, RemoveModelAsync.
## src/Infrastructure/Services/OnboardingService.cs - 107 lines - Onboarding Service with Step Navigation
  - IOnboardingService for first-run onboarding with step navigation and persistence. Next, Previous, Skip, CompleteAsync, Show.
## src/Infrastructure/Services/OomRecoveryService.cs - 190 lines - OOM Recovery Service with Progressive Degradation
  - IOomRecoveryService with MemoryUsageStatus record. CheckAndRecoverAsync, ReducePrecisionAsync, EvictLowPriorityModelAsync, EmergencyCleanupAsync.
## src/Infrastructure/Services/OpenLmStudioLogScope.cs - 148 lines - Structured Log Scope with Correlation ID Extensions
  - OpenLmStudioLogScope with correlation IDs, component context, session tracking. OpenLmStudioLoggingExtensions with BeginScopeWithCorrelation, LogOpenLmStudioEvent.
## src/Infrastructure/Services/PerformanceBenchmarkService.cs - 232 lines - Performance Benchmark Service
  - BenchmarkResult records and benchmarking for model load, chat completion, image generation. BenchmarkModelLoadAsync, BenchmarkChatCompletionAsync, BenchmarkImageGenerationAsync.
## src/Infrastructure/Services/PinguCore.cs - ~2600 lines - Consolidated Pingu System (12 partial classes)
   - PinguMeshGenerator (~350 lines): Mesh, bone hierarchy, animation clips, texture atlas generation.
   - PinguBoneLoader (~100 lines): JSON serialization for bones, animations, physics.
   - PinguAnimationStateMachine (~200 lines): State machine with blending between states.
   - PinguBehaviorTriggers (~130 lines): Weighted behavior triggers (Twitch, HeadTurn, Scratch, EarFlick, Blink, SittingDown, SittingUp).
   - PinguInverseKinematics (~140 lines): CCD IK solver for limbs.
   - PinguAnimationSystem (~300 lines): Real-time animation & physics solver (IK, bone transforms).
   - PinguPhysicsSolver (~120 lines): Distance constraints, velocity damping, gravity.
   - PinguToolHolder (~110 lines): Tool attachment system.
   - PinguNPCManager (~160 lines): NPC character management, tasks, roles, movement.
   - PinguSystemPrompts (~80 lines): Static system prompts (TaskOrchestrator, TaskOrchestratorCompressed, Assistant).
   - PinguService (~120 lines): Orchestrator wrapping all subsystems.
## src/Infrastructure/Services/PinguTools.cs - 662 lines - Consolidated Pingu Tools (6 tools)
   - 6 Pingu UI control tools: PinguTabSwitchTool, PinguPanelToggleTool, PinguGameIntegrationTool, PinguModelLoadTool, PinguModelTool, PinguWanderingTool.
## src/Infrastructure/Services/PinguRenderer.cs - 651 lines - SkiaSharp Renderer for Pingu
   - Renders Pingu characters using SkiaSharp on an Avalonia canvas. Initialize, Render, Resize, Dispose, DrawHomeScene, DrawPenguin, DrawMesh, DrawPenguinFallback.
## src/Infrastructure/Services/PluginRegistry.cs - 531 lines - Plugin Registry with Remote Registry, Sandbox Policies
  - IPluginRegistry implementation for plugin discovery, installation, and updates. SetRegistryUrl, GetSandboxPolicyAsync, InstallPluginAsync, UninstallPluginAsync.
## src/Infrastructure/Services/PluginSecurityValidator.cs - 122 lines - Plugin Provenance and Manifest Integrity Validator
  - IPluginSecurityValidator for SHA256 hash verification and manifest integrity checks. VerifyPluginHashAsync, VerifyManifestIntegrity, VerifyPluginArchiveAsync.
## src/Infrastructure/Services/RateLimitMiddleware.cs - 184 lines - Rate Limiting Middleware with Sliding Window Counter
  - RateLimitMiddleware (429 responses, X-RateLimit headers), RateLimitResult, InMemoryRateLimitService, RateLimitMiddlewareExtensions. Default 60 req/min.
## src/Infrastructure/Services/SafetensorParser.cs - 453 lines - Safetensors File Parser with SHA256/MD5 Hashing
  - Parses safetensors format (8-byte header size, JSON header, tensor metadata, sharded models). ParseHeaderAsync, ParseIndexAsync, ComputeSha256HashAsync.
## src/Infrastructure/Services/SandboxService.cs - 202 lines - Cross-Platform Process Sandbox
  - ISandboxService with cgroups v2 on Linux/macOS, Job Objects on Windows. _blockedCommands (14 commands), _restrictedEnvVars (11 vars). CreateProcessAsync, RunProcess.
## src/Infrastructure/Services/SelfSignedCertificateGenerator.cs - 497 lines - Self-Signed HTTPS Certificate Generator
  - ISelfSignedCertificateService for cross-platform HTTPS certificate generation. GenerateCertificateAsync (OpenSSL/dotnet dev-certs), TrustCertificateAsync (PowerShell).
## src/Infrastructure/Services/ServerLoadTestService.cs - 236 lines - Server Load Test Service
  - Load testing for OpenLMStudio server endpoints (chat completions, image generation, messages). RunChatLoadTestAsync, RunImageLoadTestAsync, RunMessagesLoadTestAsync.
## src/Infrastructure/Services/ServerService.cs - 2472 lines - OpenLMStudio Server with Kestrel and OpenAI-Compatible Endpoints
  - IServerService for OpenLMStudio server with Kestrel. StartAsync (WebApplication.CreateBuilder, ConfigureKestrel, CORS, rate limiting, API key auth), StopAsync.
## src/Infrastructure/Services/SqliteTaskRepository.cs - 343 lines - SQLite Task Repository with Priority Scheduling
  - ITaskRepository with SQLite-backed task persistence. CreateTaskAsync, GetTaskAsync, UpdateTaskAsync, DeleteTaskAsync, GetPendingTasks.
## src/Infrastructure/Services/SseService.cs - ~330 lines - Consolidated SSE Cluster (SseEventBuffer + SseReconnectService)
   - ISseEventBuffer + SseBufferedEvent + SseEventBuffer (event buffering for reconnection replay). ISseReconnectService + StreamedChatSession + SseReconnectService (session tracking). SseServiceExtensions (AddSseEventBuffering, AddSseReconnectTracking).
## src/Infrastructure/Services/SyntaxHighlightingMarkdownRenderer.cs - 99 lines - Markdown Renderer with Syntax Highlighting
  - IMarkdownRenderer with Markdig-based rendering. Render (Markdig→HTML with code block language detection), ExtractPlainText.
## src/Infrastructure/Services/SystemAIClient.cs - 318 lines - System AI Client for llama.cpp Server Communication
  - ISystemAIClient for System AI (llama.cpp) inference engine. StartAsync (spawns llama-server), ChatAsync (HTTP POST streaming with SSE chunks).
## src/Infrastructure/Services/SystemAIManager.cs - 527 lines - System AI Manager for System AI Lifecycle
  - SystemAI lifecycle: engine binary download → model loading → server start → streaming. LoadModelAsync, SwitchActiveModel, UnloadModel, StartServerAsync.
## src/Infrastructure/Services/TaskContextStore.cs - 934 lines - SQLite-Backed Task Context Snapshot Store
  - ITaskContextStore with SQLite-backed persistence for agentic task context snapshots. CreateAsync, GetByTaskIdAsync, UpdateAsync, DeleteAsync, ListAsync.
## src/Infrastructure/Services/TaskService.cs - ~1200 lines - Consolidated Task Service (8 classes → 1 file)
   - Consolidated from: TaskService, TaskScheduler, TaskSchedulerService, TaskCompletionDetector, TaskContextInheritor, TaskContextPruner, TaskContextReinjectionService, TaskProgressTracker.
   - **TaskService** (outer): ITaskService with create/start/pause/abort/resume, agent lifecycle, context inheritance.
   - **TaskCompletionDetector** (nested): AI-powered completion detection via TaskValidationService. DetectCompletionAsync, QuickHeuristicCheck.
   - **TaskContextInheritor** (nested): Parent→Child context propagation with budget-aware filtering. CreateChildInheritanceAsync, RequestAdditionalContextFromParentAsync, FilterByRelevance.
   - **TaskContextPruner** (nested): Archive/CompressAndArchive/Discard strategies.
   - **TaskContextReinjectionService** (nested): Fast reinjection from compressed snapshot in <100ms. FastReinjectAsync, ResumeToolCallChainAsync, GetReinjectionSummaryAsync.
   - **TaskProgressTracker** (nested): Stage/progress tracking with ConcurrentDictionary. UpdateStageAsync, ReportProgressAsync, RecordToolCallAsync, RecordErrorAsync.
   - **TaskSchedulerService** (nested): SQLite-backed task scheduling with Tasks/ToolCalls/Branches tables. InjectTasksAsync, GetScheduledTasksAsync (priority-ordered), CheckAndStartDependentTasksAsync, MapTask.
   - **TaskScheduler** (nested): In-memory wrapper around TaskSchedulerService with branch task caching. InjectTasksAsync, UpdateTaskStatusAsync, AbandonBranchAsync, PauseBranchAsync, ResumeBranchAsync.
## src/Infrastructure/Services/TaskValidationService.cs - 142 lines - AI-Powered Task Completion Validator
  - ITaskValidationService for validating task completion using Pingu (System AI). ValidateTaskCompletionAsync (ModelId: "default"), ValidateStructuredOutputAsync. ParseValidationResponse uses word-boundary-aware PASS detection.
## src/Infrastructure/Services/Tools.cs - ~1,000 lines - Consolidated Tools (11 classes → 1 file)
   - **ToolHelpers** (static): `TryGetString()`, `TryGetInt()` — shared helpers replacing duplicate methods across 5+ tools.
   - **CodeDefinitionExtractorTool** (~202 lines): `ITool` with `IGitRepositoryService`. Extracts classes/functions/methods from C#, Python, TypeScript, JavaScript. Tool name "code_definitions".
   - **CommandExecuteTool** (~93 lines): `ITool`, `IDisposable` with `ILogger<T>` and `ICommandExecutionService`. Runs shell commands in sandbox. Tool name "CommandExecute". Parses Command/Timeout/EnvironmentVariables.
   - **FilePatchTool** (~92 lines): `ITool`, `IDisposable` with `ILogger<T>` and `IFileOperationsService`. Safe file patching (add/remove lines). Tool name "FilePatch". Parses FilePath/LinesToAdd/LinesToRemove.
   - **FileReadTool** (~82 lines): `ITool`, `IDisposable` with `ILogger<T>` and `IFileOperationsService`. Reads file contents within sandbox. Tool name "FileRead". Parses FilePath/MaxLines.
   - **FileWriteTool** (~88 lines): `ITool`, `IDisposable` with `ILogger<T>` and `IFileOperationsService`. Writes/creates files within sandbox. Tool name "FileWrite". Parses FilePath/Content/Append.
   - **GitDiffTool** (~46 lines): `ITool` with `IGitRepositoryService`. Unified diff between two Git refs. Tool name "git_diff". Parses repo_path/old_ref/new_ref.
   - **GitHistoryTool** (~46 lines): `ITool` with `IGitRepositoryService`. Lists recent git commits with author, date, message. Tool name "git_history".
   - **GitBlameTool** (~46 lines): `ITool` with `IGitRepositoryService`. Line-by-line blame annotation for a file. Tool name "git_blame".
   - **GitBranchesTool** (~46 lines): `ITool` with `IGitRepositoryService`. Lists branches, tags, and remotes. Tool name "git_branches".
   - **ProjectExplorerTool** (~73 lines): `ITool`, `IDisposable` with `ILogger<T>` and `IFileOperationsService`. Recursive directory listing. Tool name "ProjectExplorer". Parses RootPath/IncludeHiddenFiles.
   - **SearchFilesTool** (~85 lines): `ITool`, `IDisposable` with `ILogger<T>` and `IFileOperationsService`. Regex search across project files. Tool name "SearchFiles". Parses Pattern/RootPath/MaxResults.
## src/Infrastructure/Services/ToolRegistry.cs - 101 lines - Central Tool Discovery and Instantiation Registry
  - IToolRegistry and IDisposable for managing tool discovery and instantiation. GetTools, Register, Unregister, GetTool (lazy DI loading), ExecuteToolAsync.
## src/Infrastructure/Services/UpdateManager.cs - 227 lines - Application Update Manager with GitHub Releases Integration
  - IUpdateManager for application updates via GitHub Releases API. CheckForUpdateAsync, DownloadUpdateAsync, ApplyUpdateAsync, CancelUpdate.
## src/Infrastructure/Services/VaEPipelineService.cs - 434 lines - ONNX Runtime VAE Pipeline for Latent Encoding/Decoding
  - IVAEPipelineService with ONNX Runtime-based VAE inference. EncodeAsync, DecodeAsync, GetAvailableModelsAsync, LoadModelAsync, SaveModelAsync.
## src/Infrastructure/Services/WindowSettingsService.cs - 71 lines - Window State Persistence Service
  - IWindowSettings and IDisposable for JSON-based window state persistence. SaveAsync, LoadAsync. Settings stored in AppData/Metadata directory.
## src/Infrastructure/Services/SystemAICoordinator.cs - 213 lines - System AI Orchestrator for Cross-Architecture Workflows
  - ISystemAICoordinator for orchestrating System AI with QEMU VMs. HandleCommandAsync, GetOrCreateArchVMAsync, ExecuteBugFixingWorkflowAsync, ExecuteCrossCompilationWorkflowAsync.
## src/Infrastructure/Services/ToolchainRegistry.cs - 91 lines - Architecture-Specific Compiler Toolchain Registry
  - IToolchainRegistry for downloading and caching architecture-specific compiler toolchains. GetToolchainAsync (downloads from GitHub releases).
