## src/Application/DependencyInjection.cs - 519 lines - Application Dependency Injection Configuration
  - Static class providing DI registrations for the Application layer. Three methods: AddApplicationTypes, ConfigureOpenApiEndpoints, AddOpenLMStudioServices. Registers 26 tool definitions and context management services.
## src/Application/Interfaces/IAccessibilityService.cs - 66 lines - Accessibility and Keyboard Navigation Interfaces
  - Defines IAccessibilityService and IKeyboardNavigationService interfaces with properties for screen reader, high contrast, font size, and keyboard navigation support.
## src/Application/Interfaces/IActiveProjectWatcher.cs - 66 lines - Active Project Tree Watcher
  - Defines ProjectTreeNode, ProjectTreeChange, ProjectTreeChangeType, and IActiveProjectWatcher interface for monitoring project tree changes.
## src/Application/Interfaces/IActivityTracer.cs - 64 lines - Agent Activity Tracing Service
  - Defines AgentToolCallTrace record and IActivityTracer interface for tracing agent tool calls with timing and resource consumption.
## src/Application/Interfaces/IAgent.cs - 267 lines - Agent and Tool Management Interfaces
  - Defines IAgent, ITaskProgressTracker, ITool, IAgentProtocolService interfaces plus related records and enums for agent lifecycle and tool management.
## src/Application/Interfaces/IAgentProgressSummaryService.cs - 38 lines - Agent Progress Summary Generation
  - Defines TaskProgressSummary record and IAgentProgressSummaryService interface for generating progress summaries and reports.
## src/Application/Interfaces/IAgentTaskAutoApprover.cs - 47 lines - Agent Task Auto-Approval Interface
  - Defines IAgentTaskAutoApprover interface with methods for auto-approving tools and commands based on configuration rules.
## src/Application/Interfaces/IAgentTaskCheckpointService.cs - 54 lines - Agent Task Checkpoint Management
  - Defines IAgentTaskCheckpointService interface with methods for saving, retrieving, and deleting task checkpoints.
## src/Application/Interfaces/IAgentTaskContextManager.cs - 49 lines - Agent Task Context Management
  - Defines IAgentTaskContextManager interface with methods for history truncation, summarization, file caching, and context budget.
## src/Application/Interfaces/IAgentTaskHookService.cs - 65 lines - Agent Task Hook Management
  - Defines IAgentTaskHookService interface with 4 lifecycle hooks (TaskComplete, UserPromptSubmit, ToolCall, StateChange) and registration methods.
## src/Application/Interfaces/IAgentTaskManager.cs - 136 lines - Central Agent Task Manager
  - Defines IAgentTaskManager interface with CurrentState, CurrentSettings, CurrentTaskId properties and 13+ methods for task lifecycle management.
## src/Application/Interfaces/IAgentTaskProgressService.cs - 49 lines - Agent Task Progress Tracking
  - Defines IAgentTaskProgressService interface with methods for progress updates, checklist management, and step tracking.
## src/Application/Interfaces/IAgentTaskStateService.cs - 34 lines - Agent Task State Persistence
  - Defines IAgentTaskStateService interface with methods for saving, restoring, and managing task state.
## src/Application/Interfaces/IArchPromptService.cs - 19 lines - Architecture-Specific QEMU System Prompts
  - Defines IArchPromptService interface with GetSystemPrompt and GetCrossCompileEnvVars methods for QEMU architectures.
## src/Application/Interfaces/IChatCompletionService.cs - 46 lines - Chat Completion Request Types and Service Interface
  - Defines ChatRequest, ChatResponseChoice records and IChatCompletionService interface with GetCompletionAsync and GetStreamingCompletionAsync methods.
## src/Application/Interfaces/IChatContextManager.cs - 44 lines - Per-Chat Conversation Context Manager
  - Defines IChatContextManager interface with methods for context compression, pinning, suppression, and custom context injection.
## src/Application/Interfaces/IChatService.cs - 47 lines - Chat Conversation Management Service Interface
  - Defines IChatService interface with CreateChatAsync, GetChatAsync, ListChatsAsync, DeleteChatAsync, AddMessageAsync methods.
## src/Application/Interfaces/IContextAwareSuggestionService.cs - 34 lines - Context-Aware Action Suggestions
  - Defines IContextAwareSuggestionService and ContextSuggestion record for providing context-aware suggestions.
## src/Application/Interfaces/IContextCompressionService.cs - 40 lines - Context Compression Service Interface
  - Defines CompressionConfig class and IContextCompressionService interface with CompressConversationAsync, GenerateFullContext, GetCompressionStats methods.
## src/Application/Interfaces/IContextCompressor.cs - 33 lines - Context Compression Strategy Interface
  - Defines IContextCompressor interface and CompressionResult record with CompressAsync and DecompressAsync methods.
## src/Application/Interfaces/IContextManipulator.cs - 72 lines - Context Segment Manipulation Service
  - Defines ContextManipulationAction enum, ContextManipulationRequest record, and IContextManipulator interface with 5 manipulation actions.
## src/Application/Interfaces/IContextRelevanceEngine.cs - 39 lines - Context Relevance Scoring Engine
  - Defines RelevanceScore record and IContextRelevanceEngine interface with ScoreSegmentsAsync, CalculateRelevanceThreshold, OrderByRelevance methods.
## src/Application/Interfaces/IContextWindowBudgeter.cs - 33 lines - Context Window Budget Management Interface
  - Defines IContextWindowBudgeter interface for managing token budget with auto-eviction of lowest-relevance segments.
## src/Application/Interfaces/IConversationEncryption.cs - 25 lines - Conversation Data Encryption Interface
  - Defines IConversationEncryption interface for AES-256-GCM conversation data encryption with PBKDF2 key derivation.
## src/Application/Interfaces/IConversationManager.cs - 340 lines - Conversation Manager, Device Monitor, Server, and MCP Interfaces
  - Defines ServerConfiguration, IConversationManager, IDeviceMonitor, IServerService, IMcpClient interfaces plus related types.
## src/Application/Interfaces/IDiffusionPipelineService.cs - 249 lines - Diffusion Pipeline, VAE, LoRA, and Embedding Services
  - Defines IDiffusionPipelineService, IVAEPipelineService, ILoraAdapterManager, IEmbeddingPipelineService, IDiffusionModelFamilyService interfaces.
## src/Application/Interfaces/IDigitalSignatureVerifier.cs - 23 lines - Digital Signature Verification Service
  - Defines IDigitalSignatureVerifier interface for verifying RSA signatures on model files with SHA256/SHA512 support.
## src/Application/Interfaces/IDownloadManager.cs - 174 lines - Model Download Manager with Progress Events
  - Defines download progress types and IDownloadManager interface for downloading model files from HuggingFace and arbitrary URLs.
## src/Application/Interfaces/IEngineLogger.cs - 101 lines - Engine Logging Service with SSE Parsing
  - Defines EngineLogEntry, EngineLoggerConfig, EngineLoggingSession classes and IEngineLogger interface for structured engine logging.
## src/Application/Interfaces/IFileOperations.cs - 228 lines - File Operations, Command Execution, and Git Services
  - Defines file/command/git operation types and IFileOperationsService, ICommandExecutionService interfaces.
## src/Application/Interfaces/IFilePreviewService.cs - 17 lines - File Preview Service Interface
  - Defines IFilePreviewService interface for previewing file contents in the agent sandbox.
## src/Application/Interfaces/IGitRepositoryService.cs - 175 lines - Git Repository Service Interface
  - Defines Git repository operation types and IGitRepositoryService interface with branch/tag/commit/diff/blame operations.
## src/Application/Interfaces/IImagePostProcessing.cs - 71 lines - Image Post-Processing Service
  - Defines IImagePostProcessingService interface with UpscaleAsync, HiResFixAsync, PreprocessControlAsync, ExtractFaceEmbeddingsAsync methods.
## src/Application/Interfaces/IInteractiveHelpService.cs - 37 lines - Interactive Help Service with Topic Search and Keyboard Navigation
  - Defines IInteractiveHelpService interface with GetHelpAsync, SearchHelpAsync, ListTopicsAsync, AddTopicAsync methods.
## src/Application/Interfaces/IKeyboardShortcuts.cs - 64 lines - Keyboard Shortcut Definitions and Mappings
  - Defines IKeyboardShortcuts interface with DefaultShortcuts (11 shortcuts), RegisterShortcut, UnregisterShortcut, GetShortcuts methods.
## src/Application/Interfaces/IMemoryManager.cs - 63 lines - GPU VRAM and CPU Memory Manager
  - Defines IMemoryManager interface for managing GPU VRAM and CPU memory allocations across loaded models with eviction scoring.
## src/Application/Interfaces/IModelCacheCleanup.cs - 62 lines - Model Cache Cleanup Service Interface
  - Defines ModelCacheRetentionPolicy enum and IModelCacheCleanupService interface for model cache maintenance.
## src/Application/Interfaces/IModelLoadingEngine.cs - 271 lines - Unified Model Loading Engine and Memory Manager
  - Defines IModelLoader, IModelManager interfaces with ContextLengthConfig, ModelLoadingConfig, ImageGenParams records.
## src/Application/Interfaces/IModelMetadataService.cs - 67 lines - Model Metadata Service Interface
  - Defines IModelMetadataService interface for model metadata operations with extraction, update, and listing methods.
## src/Application/Interfaces/IModelRepository.cs - 126 lines - GGUF Model Repository Interface
  - Defines IModelRepository interface for discovering and managing GGUF model files on disk with multi-modal support.
## src/Application/Interfaces/IModelService.cs - 75 lines - Model Management Service Interface
  - Defines IModelService interface for model management operations with discovery, metadata, and search methods.
## src/Application/Interfaces/IOnboardingService.cs - 133 lines - First-Run Onboarding Service with 8 Steps
  - Defines OnboardingStep record and IOnboardingService interface with 8 default steps for first-run onboarding.
## src/Application/Interfaces/IPanelService.cs - 15 lines - Panel Visibility Toggle Service
  - Defines IPanelService interface with TogglePanelAsync method for toggling panel visibility in the main window UI.
## src/Application/Interfaces/IPinguAutomation.cs - 38 lines - Pingu Automation and Animation Interface
  - Defines IPinguAutomation interface for Pingu automation actions and animations.
## src/Application/Interfaces/IPinguPromptGenerator.cs - 49 lines - Pingu System Prompt Generator
  - Defines PinguPromptContext record and IPinguPromptGenerator interface with GenerateFullPrompt, GenerateCompressedPrompt, GenerateForTaskType methods.
## src/Application/Interfaces/IPinguStore.cs - 89 lines - Reactive Pingu State Store
  - Defines IPinguStore reactive state store for Pingu System AI avatar with mood, panel, and animation state management.
## src/Application/Interfaces/IPluginSecurityValidator.cs - 25 lines - Plugin Security Validator
  - Defines IPluginSecurityValidator interface for plugin security validation with hash verification and manifest integrity.
## src/Application/Interfaces/IProjectExplorer.cs - 30 lines - Project Directory Explorer Service
  - Defines IProjectExplorer interface for exploring project directory and building file/folder tree.
## src/Application/Interfaces/IQEMUProcessManager.cs - 54 lines - QEMU Virtual Machine Process Manager
  - Defines IQEMUProcessManager interface for managing QEMU VM instances with QMP protocol support.
## src/Application/Interfaces/IResourceManager.cs - 48 lines - Resource Monitoring and Management Interface
  - Defines ResourceMetrics, VmResource classes and IResourceManager interface for resource monitoring.
## src/Application/Interfaces/ISelfSignedCertificateService.cs - 31 lines - Self-Signed Certificate Generation Interface
  - Defines ISelfSignedCertificateService interface for generating and managing self-signed HTTPS certificates.
## src/Application/Interfaces/ISystemAIClient.cs - 52 lines - System AI Client Interface
  - Defines ISystemAIClient interface for the llama.cpp System AI inference engine with SSE streaming support.
## src/Application/Interfaces/ISystemAICoordinator.cs - 35 lines - System AI Workflow Orchestrator
  - Defines ISystemAICoordinator interface for orchestrating System AI with QEMU VMs for cross-architecture workflows.
## src/Application/Interfaces/ITabService.cs - 25 lines - Tab Navigation Service
  - Defines ITabService interface with ActiveTab, SwitchTabAsync, GetAvailableTabs methods for tab navigation.
## src/Application/Interfaces/ITaskCompletionDetector.cs - 18 lines - Task Completion Detection Interface
  - Defines ITaskCompletionDetector interface for detecting when an agent task is complete.
## src/Application/Interfaces/ITaskContextInheritor.cs - 29 lines - ITaskContextInheritor Interface
  - Defines ITaskContextInheritor interface for parent-to-child task context inheritance.
## src/Application/Interfaces/ITaskContextPruner.cs - 18 lines - ITaskContextPruner Interface
  - Defines ITaskContextPruner interface for managing context pruning strategies when a task completes.
## src/Application/Interfaces/ITaskContextReinjectionService.cs - 38 lines - ITaskContextReinjectionService Interface
  - Defines ITaskContextReinjectionService interface for fast task context re-injection in <100ms.
## src/Application/Interfaces/ITaskContextStore.cs - 40 lines - ITaskContextStore Interface
  - Defines ITaskContextStore repository interface for managing agentic task context snapshots.
## src/Application/Interfaces/ITaskRepository.cs - 52 lines - ITaskRepository Interface
  - Defines ITaskRepository interface for CRUD operations on agentic tasks with priority scheduling.
## src/Application/Interfaces/ITaskScheduler.cs - 73 lines - ITaskScheduler Interface
  - Defines ITaskScheduler interface for managing ordered task queue with priority-aware scheduling and dependency resolution.
## src/Application/Interfaces/ITaskService.cs - 58 lines - ITaskService Interface
  - Defines ITaskService interface for managing agentic tasks with creation, execution, dependency tracking, and persistence.
## src/Application/Interfaces/ITaskValidationService.cs - 34 lines - ITaskValidationService Interface
  - Defines ITaskValidationService interface for AI-powered task validation using System AI (Pingu).
## src/Application/Interfaces/ITokenEstimator.cs - 36 lines - ITokenEstimator Interface
  - Defines ITokenEstimator interface for token estimation operations with standardized counting.
## src/Application/Interfaces/IToolchainRegistry.cs - 14 lines - Architecture-Specific Toolchain Registry
  - Defines IToolchainRegistry interface with GetToolchainAsync method for discovering compiler toolchains.
## src/Application/Interfaces/IToolRegistry.cs - 27 lines - Tool Registration Interface
  - Defines IToolRegistry interface for managing available tools during agent execution.
## src/Application/Interfaces/IUpdateManager.cs - 59 lines - Application and Plugin Update Manager
  - Defines IUpdateManager interface with CheckForUpdateAsync, DownloadUpdateAsync, ApplyUpdateAsync methods.
## src/Application/Interfaces/IVMStore.cs - 39 lines - Reactive VM Instance Store
  - Defines IVMStore reactive store interface for managing QEMU VM instances.
## src/Application/Interfaces/IWindowSettings.cs - 50 lines - Window State Persistence Interface
  - Defines IWindowSettings interface with WindowStateSettings record for managing window state persistence.
## src/Application/OpenLMStudio.Application.csproj - 25 lines - Application Layer Project File
  - .NET 8 SDK-style project file with Markdig, Microsoft.Extensions, Microsoft.ML.OnnxRuntime, System.IO.Abstractions packages.
## src/Application/Services/ActTools.cs - 230 lines - Consolidated Tool Classes (ActModeRespond, AttemptCompletion, PlanModeRespond, QuestionService)
   - ActModeRespond: act_mode_respond tool for ACT MODE with consecutive call detection.
   - AttemptCompletion: attempt_completion tool with command execution support.
   - PlanModeRespond: plan_mode_respond tool for PLAN MODE.
   - QuestionService: ask_followup_question tool with optional options.
## src/Application/Services/AgentSessionService.cs - 133 lines - Agent Session Lifecycle Manager
  - Service managing AgentSession CRUD with thread-safe lock. Methods: CreateSession, GetOrCreateSession, GetActiveSession, PauseActiveSession, ResumeSession, CompleteActiveSession, DisposeSession, ListSessions.
## src/Application/Services/AgentTaskAutoApprover.cs - 134 lines - Agent Task Auto-Approver Implementation
  - Implementation of IAgentTaskAutoApprover using AgentAutoApprovalSettings for tool/command auto-approval rules.
## src/Application/Services/AgentTaskCheckpointService.cs - 175 lines - Agent Task Checkpoint Service Implementation
  - File-based implementation of IAgentTaskCheckpointService with ConcurrentDictionary cache.
## src/Application/Services/AgentTaskContextManager.cs - 140 lines - Agent Task Context Manager Implementation
  - File-based implementation of IAgentTaskContextManager with history, file cache, and context budget support.
## src/Application/Services/AgentTaskHookService.cs - 176 lines - Agent Task Hook Service Implementation
  - ConcurrentDictionary-based hook storage keyed by taskId/hookName with 4 lifecycle hooks.
## src/Application/Services/AgentTaskManager.cs - 463 lines - Agent Task Central Orchestrator
  - Central orchestrator for agent task lifecycle with 13+ methods for init, execute, complete, terminate operations.
## src/Application/Services/AgentTaskProgressService.cs - 160 lines - Agent Task Progress Service Implementation
  - File-based implementation of IAgentTaskProgressService with checklist management and step tracking.
## src/Application/Services/AgentTaskStateService.cs - 141 lines - Agent Task State Service Implementation
  - File-based implementation of IAgentTaskStateService for saving, restoring, and managing task state.
## src/Application/Services/AgentTools.cs - 185 lines - Consolidated Tool Classes (UseSkill, UseSubagents, NewTask, PatchService)
   - UseSkill: use_skill tool for skill activation.
   - UseSubagents: use_subagents tool for parallel subagent execution (max 5).
   - NewTask: new_task tool for creating tasks with preloaded context.
   - PatchService: apply_patch tool for V4A diff format patches (ADD/UPDATE/DELETE/MOVE) with PatchOperation enum and PatchOperationInfo record.
## src/Application/Services/AgentToolExecutor.cs - 426 lines - Agent Tool Executor (26 tools)
  - Tool executor with ExecuteAsync switch routing 26 tools including write_to_file, replace_in_file, read_file, search_files, list_files, execute_command, browser_action, use_mcp_tool, and more.
## src/Application/Services/BrowserService.cs - 148 lines - Browser Service (6 Actions)
  - Implementation of IBrowserService with 6 actions: launch, click, type, scroll_down, scroll_up, close.
## src/Application/Services/CommandExecutor.cs - 120 lines - CLI Command Executor
  - Implementation of ICommandExecutor with ExecuteAsync and CancelAsync methods.
## src/Application/Services/ContextCompressor.cs - 158 lines - Context Segment Compressor (3 Levels)
  - Implementation of IContextCompressor with 3 strategies: Light (>0.3), Medium (>0.5), Aggressive (>0.7).
## src/Application/Services/ContextManipulator.cs - 188 lines - Context Segment Manipulator (5 Actions)
  - Implementation of IContextManipulator with Pin, Unpin, SuppressToggle, RemoveFromContext, AddCustomContext actions.
## src/Application/Services/ContextRelevanceEngine.cs - 116 lines - Context Relevance Scoring Engine
  - Engine scoring context segment relevance to goal text via word overlap + exact match similarity.
## src/Application/Services/ContextSnapshotManager.cs - 153 lines - Context Snapshot Manager
  - Manager for task context snapshots with file-based + ConcurrentDictionary storage.
## src/Application/Services/ContextWindowBudgeter.cs - 165 lines - Context Window Budget Manager
  - Implementation of IContextWindowBudgeter with auto-eviction of lowest-relevance segments.
## src/Application/Services/FileSystemService.cs - 460 lines - Agent File System Service
  - Implementation of IFileSystemService with write_to_file, read_file, search_files, list_files, replace_in_file. Supports .agentignore and @workspace:path syntax.
## src/Application/Services/IMarkdownRenderer.cs - 21 lines - Markdown Rendering Interface
  - Minimal interface with Render and ExtractPlainText methods.
## src/Application/Services/MarkdownRenderer.cs - 87 lines - Markdig Markdown Renderer
  - IMarkdownRenderer implementation using Markdig with AdvancedExtensions, Abbreviations, Mathematics, YamlFrontMatter.
## src/Application/Services/McpService.cs - 190 lines - MCP Service
  - Implementation of IMcpService with UseToolAsync, AccessResourceAsync, LoadDocumentationAsync methods.
## src/Application/Services/SystemPromptGenerator.cs - 213 lines - System Prompt Generator
  - Generates system prompts with 12 dynamic template variables including AGENT_ROLE, TOOL_DEFINITIONS, FILE_STRUCTURE, ENVIRONMENT_DETAILS.
## src/Application/Services/TokenEstimator.cs - 108 lines - Token Estimation Service
  - ITokenEstimator implementation with 3 estimation algorithms plus GetHumanReadableTokenCount.
## src/Application/Services/WebFetchService.cs - 78 lines - Web Fetch Tool
  - Implementation of IWebFetchService with HTTP/HTTPS support and automatic HTTPS upgrade.
## src/Application/Services/WebSearchService.cs - 122 lines - Web Search Service
  - Implementation of IWebSearchService with FetchAsync and SearchAsync methods with domain filtering.
## src/Application/Types/AgentTypes.cs - 154 lines - Agent Tool Request Records
  - 9 record types for agent tool requests: ListFilesRequest, WriteFileRequest, ReadFileRequest, ReplaceFileRequest, SearchFilesRequest, ExecuteCommandRequest, UseMcpToolRequest, AccessMcpResourceRequest, BrowserActionRequest.
## src/Application/Types/ChatTypes.cs - 242 lines - Chat Completion Types and Event Handlers
  - ChatMessage, ChatCompletionRequest, ChatChoice, ChatCompletionResponse, UsageStats, StreamingEventArgs, StreamingEventHandler.
## src/Application/Types/ImageTypes.cs - 289 lines - Image Generation Request/Response Types
  - OpenAI-compatible image generation types: OpenAIImageGenerationRequest, ImageGenerationResponse, ImageData, ImageInpaintingRequest, ImageOutpaintingRequest.
## src/Application/Types/MiscTypes.cs - 295 lines - Misc DTOs, Plugin Verification, HuggingFace Info, Budget Types, and OpenTelemetry ActivitySource
  - FilePreviewResult, PluginVerificationResult, HfRepoFileInfo, LoadTestResult, BenchmarkResult, ChatBudgetStateDto, ContextBudgetIndicator, OpenLmStudioActivitySource.