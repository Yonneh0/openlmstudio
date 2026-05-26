## src/Infrastructure/ActivityTracer.cs - Agent Activity Tracer Implementation
  - 93-line implementation class providing IActivityTracer interface for tracing agent tool calls with timing and resource consumption. Uses global using directives (System, System.Collections.Concurrent, System.Collections.Generic, System.Linq, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces). Contains ConcurrentDictionary<string, List<AgentToolCallTrace>> for per-task trace storage. Methods: RecordToolCall (logs trace with task ID, tool name, duration, CPU time, memory delta), GetRecentToolCalls (takes taskId, count, returns IReadOnlyList<AgentToolCallTrace>), GetTaskStatistics (returns TaskCallStatistics with total/successful/failed calls, avg/total duration, CPU/memory/disk totals), StartSpan (creates Activity with agent.task.id tag), CompleteSpan (sets success/error tags, stops activity). Uses ActivitySource named "OpenLMStudio.ActivityTracer" for distributed tracing. Includes IDisposable pattern with _disposed flag. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/ArchPromptService.cs - Architecture-Specific QEMU System Prompt Service
  - 119-line implementation class providing IArchPromptService for architecture-specific system prompts and cross-compile variables for QEMU VMs. Contains static dictionary _systemPrompts with 13 architecture entries (X86_64, AArch64, RISC_V64, AVR, MIPS, MIPS64, MIPSEL, MIPS64EL, PPC, PPC64, SPARC, SPARC64, I386, ARMv7L) each with build commands, CC/CXX variables, and known issues. Contains static dictionary _crossCompileEnv with 14 entries mapping each architecture to its cross-compile environment variables (e.g., CC=gcc CXX=g++ for x86_64, CC=aarch64-linux-gnu-gcc CXX=aarch64-linux-gnu-g++ for ARM64). Methods: GetSystemPrompt (takes ArchitectureType, returns string), GetCrossCompileEnvVars (takes ArchitectureType, returns string). Uses GetValueOrDefault for safe dictionary lookups. Uses OpenLMStudio.Application.Interfaces and OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/DependencyInjection.cs - Infrastructure Layer Dependency Injection Configuration
  - 509-line static class providing comprehensive DI registrations for the Infrastructure layer. Contains AddInfrastructureServices (returns IServiceCollection) as primary registration method and AddOpenLMStudioServices (convenience method combining Application + Infrastructure). Registers 80+ services including: IServerService (ServerService, singleton), IConversationManager (FileConversationManager, singleton), IDeviceMonitor (WindowsDeviceMonitor, singleton), IMcpClient (McpStdioClient, singleton), IChatCompletionService (LlamaCppChatCompletionService, singleton), GgufParser (singleton), IChatService (ChatService, singleton), IModelService (ModelService, singleton), IModelMetadataService (ModelMetadataService, singleton), ITokenEstimator (TokenEstimator, singleton), IModelRepository (JsonModelRepository, singleton), IDownloadManager (DownloadManager, singleton), IDiffusionPipelineService (DiffusionPipelineService, singleton), IVAEPipelineService (VAEPipelineService, singleton), ILoraAdapterManager (LoraAdapterManager, singleton), IEmbeddingPipelineService (EmbeddingPipelineService, singleton), IMemoryManager (MemoryManager, singleton), IChatContextManager (ChatContextManager, singleton), IContextCompressor (ConversationContextCompressor, singleton), IContextRelevanceEngine (ContextRelevanceEngine, singleton), IContextManipulator (ContextManipulator, singleton), IContextWindowBudgeter (ContextWindowBudgeter, singleton), ITaskContextInheritor (TaskContextInheritor, singleton), ITaskContextReinjectionService (TaskContextReinjectionService, singleton), ITaskContextPruner (TaskContextPruner, singleton), IAgent (Agent factory), IToolRegistry (ToolRegistry, singleton), IPluginRegistry (PluginRegistry, singleton), ITaskCompletionDetector (TaskCompletionDetector, singleton), IAgentProgressSummaryService (AgentProgressSummaryService, singleton), ITaskValidationService (TaskValidationService, singleton), ITaskScheduler (TaskSchedulerService, singleton), IActivityTracer (ActivityTracer, singleton), IWindowSettings (WindowSettingsService, singleton), IPinguStore (PinguStore, singleton), IPinguPromptGenerator (PinguPromptGenerator, singleton), ISystemAIClient (SystemAIClient, singleton), IQEMUProcessManager (QEMUProcessManager, singleton), IArchPromptService (ArchPromptService, singleton), IVMStore (VMStore, singleton), IEngineLogger (EngineLogger, singleton), IContextCompressionService (ContextCompressionService, singleton), IToolchainRegistry (ToolchainRegistry, singleton), IPinguAutomation (PinguAutomation, singleton), IResourceManager (ResourceManager, singleton), ISystemAICoordinator (SystemAICoordinator, singleton). Uses Microsoft.Extensions.DependencyInjection, OpenLMStudio.Application.Interfaces/Services, OpenLMStudio.Infrastructure namespaces.
## src/Infrastructure/ModelLifecycleTracer.cs - Model Lifecycle Event Tracer
  - 115-line implementation class for tracing model load/unload timing and VRAM allocation changes. Contains NoOpLogger<T> internal class (ILogger<T>) for when DI is not configured. Contains ConcurrentDictionary<string, ModelLoadTrace> _activeLoads and ConcurrentQueue<ModelLoadTrace> _recentTraces. Methods: TrackModelLoad (takes modelId, modelType, fileSizeBytes, uses CallerMemberName), CompleteModelLoad (takes modelId, loadTimeMs, vramAllocated, success), TrackModelUnload (takes modelId), GetRecentTraces (takes count, returns IReadOnlyList<ModelLoadTrace>). ModelLoadTrace record contains ModelId, ModelType, FileSizeBytes, StartedAt, CompletedAt, LoadTimeMs, VramAllocatedBytes, Success, Caller. Keeps only last 100 traces. Uses System, System.Collections.Concurrent, System.Diagnostics, System.Runtime.CompilerServices, Microsoft.Extensions.Logging (via MLogLevel alias). Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/OpenLMStudio.Infrastructure.csproj - Infrastructure Project File
  - 33-line MSBuild project file for OpenLMStudio.Infrastructure targeting net8.0. Contains project references to Domain and Application layers. Framework reference: Microsoft.AspNetCore.App. Package references: Markdig.SyntaxHighlighting (1.1.7), Microsoft.Extensions.DependencyInjection (8.0.1), Microsoft.Extensions.Configuration.Abstractions (8.0.0), Microsoft.Extensions.Hosting.Abstractions (8.0.0), System.IO.Pipelines (8.0.0), Microsoft.Data.Sqlite.Core (9.0.0), SQLitePCLRaw.bundle_e_sqlite3 (2.1.10), System.Management (8.0.0), System.Net.Sockets (4.3.0), SkiaSharp (3.116.1), SkiaSharp.NativeAssets.Linux.NoDependencies (conditional), System.Security.Cryptography.X509Certificates (4.3.2). Enables Windows targeting and suppresses CA1416/NU1701 warnings. RootNamespace is OpenLMStudio.Infrastructure.
## src/Infrastructure/PinguAutomation.cs - Pingu Automation Implementation
  - 67-line implementation class providing IPinguAutomation interface for Pingu automation actions and animations. Contains IPinguStore and IQEMUProcessManager dependencies. Methods: EnterControlModeAsync (transitions Pingu to Working mood), HandleDragToPauseAsync (takes DragEvent, pauses all running VMs when dragged to Pingu, transitions to Idle), PerformActionAsync (takes action string, optional target Element) with switch on "startVM" (starts VM, transitions to Happy), "stopVM" (stops VM, transitions to Idle), default (transitions to Happy). Uses OpenLMStudio.Application.Interfaces and OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/PinguPromptGenerator.cs - Pingu System Prompt Generator Implementation
  - 382-line implementation class providing IPinguPromptGenerator interface for generating context-aware system prompts for Pingu. Contains ILogger dependency. Methods: GenerateFullPrompt (builds complete prompt with base identity, task-specific prompts, project state, model state), GenerateCompressedPrompt (builds compact prompt for tight context windows), GenerateForTaskType (switch on PinguTaskType: TaskOrchestrator, UIControl, ModelManagement, GamePlay, Wandering, UserAssistant, ModelRun, Workflow). Private prompt builders: BuildTaskOrchestratorPrompt (admin of shared task scheduler tree), BuildTaskOrchestratorCompressed, BuildUIControlPrompt (tab switching, panel toggling, button clicking), BuildModelManagementPrompt (load/unload/switch models), BuildGamePlayPrompt (Minesweeper, Tetris, Snake, Jezzball, Solitaire), BuildWanderingPrompt (explore UI), BuildUserAssistantPrompt (helpful direct assistant), BuildModelRunPrompt, BuildWorkflowPrompt. Uses System.Text (StringBuilder), Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/PinguStore.cs - Reactive Pingu State Store Implementation
  - 236-line implementation class providing IPinguStore interface with reactive state management. Contains PinguEvent record (Name) and PinguEventBus static event bus for cross-component communication. Contains PinguState, ILogger, Timer _blinkTimer, object _stateLock. Methods: UpdateMoodAsync (PinguMood), ToggleMenuAsync (toggles IsMenuOpen, publishes PINGU_CHAT_OPEN/CLOSE events), SetActivePanelAsync (PinguPanelType), SetAwakeAsync (bool, triggers StartAwakeningSequenceAsync), PinAndOpenChatAsync, UnpinPinguAsync, SetLoadingProgressAsync (double), SetBlinkStateAsync (bool), StartAwakeningSequenceAsync (3-phase: Shake 0.5s → Stretch 2s → Glow 2s), StartBlinkTimerAsync, StartSpeakingAsync, StartThinkingAsync, CompleteTaskAsync (happy 2s → idle), HandleTaskErrorAsync (error 5s → idle), StartWorkingAsync (working with 1.5x bob speed), IdleAsync. Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/QEMUProcessManager.cs - QEMU Virtual Machine Process Manager Implementation
  - 326-line implementation class providing IQEMUProcessManager interface for managing QEMU VM instances with QMP protocol support. Contains ConcurrentDictionary<string, VMInstance> _instances and ConcurrentDictionary<string, Process> _processes, int _qmpPortBase=9100, int _monPortBase=10000. Contains _archBinaries dictionary mapping 16 ArchitectureTypes to QEMU binaries (qemu-system-x86_64, qemu-system-aarch64, qemu-system-riscv64, etc.). Methods: CreateVMAsync (takes VMCreationConfig, builds args, starts process, registers listeners), StartVMAsync, PauseVMAsync, ResumeVMAsync, StopVMAsync, DeleteVMAsync, ExecuteQMPCommandAsync (per-connection capability negotiation with tcp socket), QueryBlockDevicesAsync (query-block QMP command), GetProcess (returns System.Diagnostics.Process). Helper methods: GetBinary, GetDefaultMachine, ExtractNumericId, BuildArgs (machine, smp, m, enable-kvm, drive, netdev, virtio-net-pci, EDK2 UEFI firmware), RegisterProcessListeners, UpdateState, SendQMPMessageAsync, ReadQMPResponseAsync (multi-line JSON accumulation). Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models, System.Diagnostics, System.Net, System.Net.Sockets, System.Text, System.Text.Json, System.Text.Json.Nodes namespaces. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/ResourceManager.cs - Resource Monitoring Implementation
  - 122-line implementation class providing IResourceManager interface with VM-aware allocation. Contains IQEMUProcessManager, System.Threading.Timer _monitorTimer, int _monitorIntervalMs=10000, static MAX_CONTEXT=131072, IContextCompressionService dependency, bool _disposed. Methods: MonitorResourcesAsync (collects CPU/memory/disk metrics and VM instances), AdjustModelSettingsAsync (takes ResourceMetrics, logs CPU pressure >90%), CompactPromptIfNeededAsync (takes contextLength, compresses if >MAX_CONTEXT). Private methods: GetCpuUsage (Process.TotalProcessorTime), GetMemoryUsage (WorkingSet64), GetDiskSpace (DriveInfo). Uses OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure namespace.
## src/Infrastructure/Services/AccessibilityService.cs - Accessibility Settings Manager
  - 94-line implementation class providing IAccessibilityService interface for managing accessibility settings across the application. Contains ILogger dependency and private fields: _screenReaderEnabled, _highContrastEnabled, _preferredFontSize (default 14), _keyboardNavigationEnabled (default true). Properties have logging setters: ScreenReaderEnabled, HighContrastEnabled, PreferredFontSize (clamped 10-24), KeyboardNavigationEnabled. Methods: GetSettings (returns IReadOnlyDictionary<string, object> with all 4 settings), ApplySettings (logs all settings together). Uses System, System.Collections.Generic, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ActiveProjectTree.cs - Project Tree Node and File Preview Services
  - 120-line file containing ProjectTreeNode record (Path, Name, IsDirectory, Children as List, SizeBytes, HasChildren, IsExpanded), ProjectTreeChangedEventArgs class (ChangeType, Path, OldPath), ProjectTreeChangeType enum (Created, Deleted, Changed, Renamed), and FilePreviewService class implementing IFilePreviewService. FilePreviewService provides GetPreviewAsync (reads first N lines, skips binary files >1MB), IsBinaryFile (checks for null bytes in first 8192 bytes). Uses System, System.Collections.Generic, System.Collections.ObjectModel, System.IO, System.Linq, System.Text, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ActiveProjectWatcher.cs - Real-Time Project Tree Change Monitor
  - 272-line implementation class providing IActiveProjectWatcher interface for monitoring real-time changes to the active project tree using FileSystemWatcher. Contains ILogger, _watchPath, IProjectExplorer?, _ignorePatterns (20 patterns: node_modules, .git, .vs, bin, obj, .idea, __pycache__, etc.), _ignoreExtensions (14 extensions: .log, .pdb, .dll, .exe, .zip, .tmp, etc.), FileSystemWatcher?, _currentTree, _debounceTimer. Debounces changes with 300ms interval. Methods: StartAsync (creates watcher with 64KB buffer, subscribes to Created/Deleted/Changed/Renamed/Error events), StopAsync (unsubscribes and disposes), RefreshAsync (rebuilds tree), BuildTreeAsync (recursive directory enumeration with cancellation). Event: TreeChanged (ProjectTreeChange). Uses System, System.Collections.Generic, System.Collections.ObjectModel, System.IO, System.Linq, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/Agent.cs - Core Agent with Plan/Act Cycle and Error Recovery
  - 684-line implementation class providing IAgent interface for agentic task execution with plan/act cycle. Contains ILogger, ITaskProgressTracker, IToolRegistry, IChatCompletionService, IChatContextManager, IContextCompressor? dependencies. Properties: State (AgentState), _consecutiveFailures (max 3), _recentActions (Queue<string> with 10-window loop detection), _lastCheckpoint (AgentCheckpoint). Methods: ExecuteAsync (generates plan via GeneratePlanAsync, executes actions in loop with ExecuteActionsAsync), PauseAsync (saves checkpoint), ResumeAsync (restores from checkpoint), AbortAsync, GetToolCalls, GetConversationHistory. Error recovery: loop detection (50%+ same action in window), consecutive failure threshold (3), fallback plan generation, degraded action generation, auto-commit for Write/Patch tools (creates agent-{taskId} branch). Tool parameter parsing via regex (path/file/directory/num/count patterns). 30-minute timeout guard. AgentCheckpoint record (Iteration, ToolCalls, ConversationHistory, State, Timestamp). Uses System.Collections.Generic, System.Threading, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AgentCommunicationProtocol.cs - Agent Communication Protocol for Plan/Act Switches
  - 257-line implementation class providing IAgentCommunicationProtocol interface for structured plan/act phase transitions. Contains AgentCommunicationPhase enum (Planning, Acting, Reviewing, Completed, Failed), AgentCommunicationMessage record (Id, Phase, Content, SenderRole, Metadata, Timestamp), PhaseTransitionEvent record (TaskId, From, To, Reason). AgentCommunicationProtocol class with _listeners HashSet<Action<PhaseTransitionEvent>> and _safeOperations dictionary (FileReadTool, SearchFilesTool, GitHistoryTool, GitDiffTool, ProjectExplorerTool). Methods: Subscribe, Unsubscribe, CreatePlanMessage (parses plan into steps), CreateActionMessage, IsSafeOperation, RequiresUserApproval, RaisePhaseTransition, CreateCompletionMessage, CreateFailureMessage, ExtractPlanSteps (parses numbered/bulleted lines). Uses System, System.Collections.Generic, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AgentProgressSummaryService.cs - Agent Progress Summary Generator
  - 94-line implementation class providing IAgentProgressSummaryService interface for generating human-readable progress summaries and completion reports. Contains ILogger dependency. Methods: GenerateSummary (takes taskId, taskDescription, toolCalls, completedPhases, startedAt, completedAt, returns TaskProgressSummary with successful/failed call counts), GenerateReport (takes TaskProgressSummary, returns formatted report string with duration, tool call stats, phases, summary). Helper methods: BuildSummary (status message based on completion), FormatDuration (smart formatting: hours/minutes/seconds). Uses System, System.Collections.Generic, System.Linq, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AgentProtocolService.cs - Agent Protocol Service with Phase Transitions and Approval Gating
  - 182-line implementation class providing IAgentProtocolService interface for agent protocol management. Contains ILogger, IChatCompletionService, IChatContextManager dependencies, _state (AgentState), _userApprovedPlan, _currentPlan. Properties: State, IsPlanApproved, CurrentPlan. Events: PhaseTransitioned (PhaseTransitionEventArgs), ApprovalRequired (AgentApprovalRequiredEventArgs). Methods: GeneratePlanAsync (builds system prompt, gets LLM plan, pauses for approval), ApprovePlanAsync (transitions to Acting), RejectPlanAsync (generates revision plan), IsActionSafeAsync (safe tools: FileRead, SearchFiles, ProjectExplorer, GitDiff, GitHistory, GitBlame), RequestActionApprovalAsync. Private: BuildSystemPrompt (assembles task description, tools, context). Uses System, System.Collections.Generic, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AgentSessionPersister.cs - Agent Session Persistence to Disk
  - 201-line implementation class for persisting and restoring agent session state to/from disk. Contains ILogger, _sessionDirectory, System.Text.Json.JsonSerializerOptions (_jsonOptions), _disposed. Session files stored as {taskId:N}.session.json. Methods: SaveSessionAsync (serializes state, toolCalls, conversationHistory, checkpoint to JSON with atomic write via temp file), LoadSessionAsync (deserializes session, returns AgentSessionData), DeleteSessionAsync, ListSessions (returns list of Guid taskIds from .session.json files). Private SessionData class (TaskId, State, ToolCalls, ConversationHistory, Checkpoint, SavedAt, LastCheckpointIteration). AgentSessionData record (State, ToolCalls, ConversationHistory, Checkpoint). Uses System, System.Collections.Generic, System.IO, System.Linq, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AgentSystemPromptGenerator.cs - Dynamic Agent System Prompt Generator
  - 254-line implementation class for dynamically assembling agent system prompts based on context and available tools. Contains _toolDescriptions (Dictionary<string, string>), _discoveredTools (HashSet<string>), _logger. Default system prompt defines autonomous agent behavior with task management, validation, and phase awareness. Methods: GeneratePlanningPrompt (task description, context, tools), GenerateActingPrompt (task, plan, completed actions, last result), GenerateNextActionSuggestions (context-aware suggestions for next action based on task keywords and phase), RegisterToolDescription, AutoDiscoverTools (via ToolDescriptionAttribute or Description property reflection), RebuildWithAutoDiscoveredTools, GetToolDescriptions, GetDiscoveredToolNames. Private: BuildToolSection (formats tool descriptions as markdown list). ToolDescriptionAttribute class (AttributeTargets.Class) for marking tool types. Uses System, System.Collections.Generic, System.Linq, System.Reflection, Microsoft.Extensions.Logging namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AgentTaskProgressTracker.cs - Agent Task Progress Tracker
  - 141-line implementation class providing ITaskProgressTracker interface for tracking agentic task progress through stages. Contains ILogger, _stage (TaskProgressStage), _progressPercentage, _iterationsExecuted, _isIterationLimitExceeded, _toolCalls (List), _errorMessage. Properties: Stage, ProgressPercentage, IsIterationLimitExceeded, HasError, ErrorMessage. Methods: UpdateStageAsync (auto-updates progress: NotStarted=0, InProgress=<85, Reviewing=90, Completed=100, Failed=0), ReportProgressAsync (validates 0-100), RecordToolCallAsync (records AgentToolCallRecord, detects timeout >5min and stuck loops >10 failures in 60s), RecordErrorAsync (updates stage to Failed), CheckIterationLimit (marks exceeded when maxIterations breached, sets progress to 85), GetToolCalls. Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ApiKeyAuthMiddleware.cs - API Key Authentication Middleware
  - 108-line file containing ApiKeyAuthMiddleware class and ApiKeyAuthMiddlewareExtensions static class. ApiKeyAuthMiddleware takes RequestDelegate and ILogger, validates API key from X-Api-Key header or api_key query parameter. Contains UnauthenticatedPaths HashSet (9 paths: /v1/health, /v1/models, image endpoints) that skip auth. InvokeAsync checks auth, returns 401 with error message on missing key. Extensions: UseApiKeyAuthentication (IApplicationBuilder), AddApiKeyAuthentication (IServiceCollection). Uses Microsoft.AspNetCore.Builder, Microsoft.AspNetCore.Http, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, OpenLMStudio.Application.Types namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AppDataDirectoryResolver.cs - Cross-Platform AppData Directory Resolver and SQLite Factory
  - 259-line file containing AppDataDirectoryResolver class and SqliteDatabaseFactory class. AppDataDirectoryResolver resolves platform-specific OpenLMStudio appdata directories: Windows (%APPDATA%\OpenLMStudio), macOS (~Library/Application Support/OpenLMStudio), Linux ($XDG_CONFIG_HOME/OpenLMStudio → ~/.config/OpenLMStudio). Contains cached _cachedAppDataPath, GetAppDataDirectory, GetSubDirectory, InitializeSubdirectories (creates contexts/metadata/models/tasks/logs), GetConversationDatabasePath, GetSettingsDatabasePath, GetTaskContextDatabasePath, TaskDirectory, MetadataDirectory, ModelsDirectory, LogsDirectory, GetLogFilePath. SqliteDatabaseFactory uses SQLitePCLRaw with double-checked locking, CreateConnection, EnsureDatabaseExistsAsync. Uses System, Microsoft.Extensions.Logging, OpenLMStudio.Domain.Models, SQLitePCL namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AppUpdateChecker.cs - GitHub Releases Update Checker
  - 156-line file containing AppUpdateInfo record, IAppUpdateChecker interface, AppUpdateChecker implementation, GithubReleaseInfo and GithubAssetInfo internal records. AppUpdateChecker uses HttpClient with 30s timeout, checks GitHub API for releases/latest, caches results for 1 hour, compares versions (strips 'v' prefix). Methods: CheckForUpdatesAsync, GetCurrentVersion, CompareVersions, GetDownloadUrl. Cache file stored at %APPDATA%\OpenLMStudio\update-cache.json. Uses System.Reflection, System.Text.Json, Microsoft.Extensions.Logging namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/AvaloniaMarkdownRenderer.cs - Markdown-to-HTML Renderer for Avalonia
  - 173-line class implementing IMarkdownRenderer with Render and ExtractPlainText methods. RenderFallback uses StringBuilder with line-by-line parsing for code blocks (fenced with ```), headers (h1-h6), unordered/ordered lists, blockquotes, horizontal rules, paragraphs. RenderInline handles inline formatting (code, bold, italic, links, images) with regex. ExtractPlainText strips HTML tags and decodes entities. Supports code block language metadata. Uses System.Text.RegularExpressions, OpenLMStudio.Application.Services namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/BinaryRegistry.cs - Local Binary Storage with Version Tracking
  - 257-line class managing local binary storage with version tracking, branch management, and volatile binary support. Contains _registryPath, _branchesPath, _volatilePath, _cache, _lock. Methods: RegisterDownloadedBinary, RegisterLocalBinary (creates manifest JSON), RegisterVolatileBinary, GetBinaries, GetBinary, RemoveBinary, GetBestForBackend, LoadRegistry, SaveRegistry (atomic via temp file), GetPlatform, GetArchitecture. BinaryInfo records include Id, Name, Backend (BackendType.Cpu), Platform (windows/linux/macos), Architecture, Version, Checksum, DownloadUrl, DownloadDate, IsBuiltLocally, GitBranch, GitCommit, BuildDate, BuildFlags, BinaryPath, ManifestPath. Uses DictionaryExtensions.AddOrUpdate. Uses Microsoft.Extensions.Logging, OpenLMStudio.Domain.Models, System.Text.Json namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ChatContextManager.cs - Per-Chat SQLite-Backed Context Manager
  - 534-line class implementing IChatContextManager with SQLite-backed persistence. Contains ILogger, AppDataDirectoryResolver, SqliteDatabaseFactory, _jsonOptions. Four SQLite tables: ChatMessages, PinSegmentStates, SuppressSegmentStates, CustomInjections. Methods: GetCompressedContextAsync (pinned segments at full detail, compressed regular segments, custom injections), PinSegmentAsync, UnpinSegmentAsync, SuppressSegmentAsync, RevealSegmentAsync, InjectCustomContextAsync, RemoveCustomContextAsync. Helper methods: GetPinnedSegmentsInternalAsync, GetSuppressedSegmentsInternalAsync, GetCustomInjectionsInternalAsync, GetAllContextSegmentsInternalAsync, EnsurePinTableExists, EnsureSuppressTableExists, EnsureCustomInjectionsTableExists, InitializeDatabasesAsync, EnsureChatMessagesTableExists, EstimateTokenCount. Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models, System.Text.Json namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ChatPersistenceService.cs - JSON-Based Chat Conversation Persistence
  - 490-line class implementing IConversationManager with JSON file-based persistence. Contains _conversationsDirectory, JsonOptions (WriteIndented, CamelCase, JsonStringEnumConverter), TokensPerCharacter=4. Methods: ListChatsAsync (scans *.json files), CreateChatAsync, LoadChatAsync (searches all subdirectories), DeleteChatAsync, AddMessageAsync (updates token count), GetMessagesAsync (with limit via TakeLast), SearchChatsAsync (name + content search), SearchMessagesInChatAsync, UpdateChatAsync (reflection-based), CalculateTotalTokenCountAsync, ExportChatAsync, ImportChatAsync, GetConversationTokenCount, RenameChatAsync. Private: SaveChatAsync (persists messages with token counts and timestamps, clears IsStreaming), EstimateTokenCount. Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models, System.Collections.Concurrent, System.Text.Json namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ChatService.cs - Chat Service Wrapping IConversationManager
  - 103-line class implementing IChatService with IConversationManager and ILogger dependencies. Methods: CreateChatAsync, GetChatAsync, ListChatsAsync, DeleteChatAsync, AddMessageAsync (creates Message with Role from string, token count, timestamp). Private: EstimateTokenCount (text.Length + 3 / 4). Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/CodeDefinitionExtractorTool.cs - Code Definition Extractor for Multiple Languages
  - 202-line class implementing ITool with IGitRepositoryService dependency. Tool name "code_definitions". Methods: ExecuteAsync (extracts definitions from repo, outputs to Console), GetParameterSchema (repo_path, max_depth, language). Private: ExtractDefinitions (supports C#, Python, TypeScript, JavaScript), ExtractCSharpDefinitions (classes with access modifiers, methods, properties), ExtractPythonDefinitions (classes, functions), ExtractJavaScriptDefinitions (classes, functions with export/async). Language filter: .cs, .py, .ts, .tsx, .js, .jsx, .java, .go, .rb, .rs. Uses System, System.Collections.Generic, System.IO, System.Linq, System.Text, System.Text.RegularExpressions, System.Threading.Tasks, OpenLMStudio.Application.Interfaces namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/CommandExecuteTool.cs - Command Execution Tool for Agent Sandbox
  - 93-line class implementing ITool and IDisposable with ILogger and ICommandExecutionService dependencies. Tool name "CommandExecute". Methods: ExecuteAsync (parses Command/Timeout/EnvironmentVariables parameters, executes via command executor), GetParameterSchema (Command required string, EnvironmentVariables optional object[], Timeout optional number), Dispose. Uses System, System.Collections.Generic, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/CommandExecutionService.cs - Sandboxed Command Execution Service
  - 128-line class implementing ICommandExecutionService with cross-platform sandboxing (cgroups v2 on Linux/macOS, Job Objects on Windows). Contains _activeProcesses Dictionary and _disposed flag. ExecuteAsync creates Process with cmd.exe/sh, redirects stdout/stderr, waits for exit with configurable timeout, returns CommandExecuteResult with exit code/output/error/duration. CancelAsync/KillAsync manage active processes. GetActiveProcesses returns SandboxProcessInfo list. GetResourceUsageAsync returns SandboxResourceUsage with CPU/working set. Uses System, System.Collections.Generic, System.Diagnostics, System.IO, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Interfaces namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/CompilationHelper.cs - Static Compilation Helper for Cross-Platform Builds
  - 118-line static class providing OS-specific CMake build script generation and prerequisite checking. Contains CompilationConfig record (ArchitectureType, SourcePath, OutputPath). Methods: GetCompileScript (generates CMake commands for Windows/macOS/Linux with appropriate flags like -DGGML_XNNPACK=ON, -DGGML_METAL=ON, -DCMAKE_OSX_ARCHITECTURES=arm64), GetInstallCompilerCommand (winget for Windows, xcode-select for macOS, apt for Linux), CheckPrerequisites (verifies git/cmake/compiler availability), GetOSName. Private: IsCommandAvailable (uses cmd /c where). Uses OpenLMStudio.Domain.Models (ArchitectureType) and System.Runtime.InteropServices namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ContextAwareSuggestionService.cs - Context-Aware Action Suggestion Engine
  - 249-line class implementing IContextAwareSuggestionService for generating next-action suggestions based on conversation state, task context, and project state. Contains _logger dependency. GetNextActionSuggestionsAsync combines three suggestion sources: GenerateConversationSuggestions (handles empty history, user/assistant messages, command indicators, conversation length), GenerateTaskSuggestions (handles all AgentState values: Idle/Planning/Acting/Paused/Completed/Failed with state-specific suggestions), GenerateProjectSuggestions (scans project state directory for code files). Sorts by confidence descending. Uses OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models, Microsoft.Extensions.Logging namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ContextCompressionService.cs - Conversation History Compression Using System AI
  - 128-line class implementing IContextCompressionService for compressing conversation history using System AI (1B CPU model). Contains _logger, _config (CompressionConfig), TokensPerChar=0.25. Methods: CompressConversationAsync (compares total tokens to budget, splits messages by token threshold, calls CompressMessages), GenerateFullContext (builds preamble from compressed history + active messages), GetCompressionStats (computes active/compressed token counts and compression ratio). Private: SplitMessagesByTokens (bottom-up token allocation), CompressMessages (identifies key decisions and file modifications), EstimateTokens. Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ContextManipulator.cs - User-Driven Context Segment Control
  - 231-line class implementing IContextManipulator for pin, unpin, suppress toggle, remove from context, and add custom context actions. Delegates to ChatContextManager for persistence. Contains _logger, _chatContextManager, _pinnedCache, _suppressedCache, _syncedChats (all ConcurrentDictionary). ManipulateAsync handles 5 actions with cache updates. GetPinnedSegmentsAsync/GetSuppressedSegmentsAsync/GetCustomInjectionsAsync query ChatContextManager with cache fallback. EnsureCacheSyncedAsync rebuilds caches from database. Uses System.Collections.Concurrent, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ContextRelevanceEngine.cs - Context Relevance Scoring with Recency/Semantics/Entity Matching
  - 260-line class implementing IContextRelevanceEngine for scoring segment relevance. Contains _logger dependency. ScoreSegmentsAsync (30% recency, 40% semantic, 30% entity match) uses ExtractKeyTerms (removes stop words, keeps words >=3 chars), CalculateRecencyScore (user proximity + length-based), CalculateSemanticRelevance (weighted term matching with sqrt normalization), ScoreEntityMatch (path patterns + code identifier matching). CalculateRelevanceThreshold (dynamic based on conversation length and remaining budget). OrderByRelevance (sorts by relevance score, user messages first). Private: CountSubstrings, ExtractCodeIdentifiers (camelCase, PascalCase, snake_case). Uses Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ContextWindowBudgeter.cs - Token Budget Management with Auto-Eviction
  - 309-line class implementing IContextWindowBudgeter for managing token budget in conversation context windows. Contains _budgetStates ConcurrentDictionary per chat. Methods: GetOrCreateBudgetAsync, DeductFromBudgetAsync (auto-evicts if negative), TryAutoEvictLowestRelevanceSegmentsAsync (evicts non-pinned lowest-relevance segments), GetBudgetIndicatorAsync (color zones: Green/Yellow/Red), SetBudgetForChatAsync, SetCompressionStrategyForChatAsync, UpdateSegmentRelevanceScoresAsync. Internal ChatBudgetState class (MaximumTokens, RemainingTokens, BudgetAllocation, CompressionStrategy, _segmentRelevanceScores, _evictionCandidates) with Deduct, MarkSegmentForEviction, SetSegmentRelevanceAsync, IsAllSegmentsPinned, GetAllocationSummary. Uses global::Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Application.Types, OpenLMStudio.Domain.Models, OpenLMStudio.Infrastructure, System.Collections.Concurrent namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ConversationContextCompressor.cs - Multi-Strategy Context Compression
  - 638-line class implementing IContextCompressor for conversation-level context compression with three strategies: Light (key phrase extraction, keeps first 10 segments), Medium (summary generation, keeps first 5), Aggressive (outline-level information, keeps first 3). Contains _logger dependency. CompressAsync separates pinned vs compressible segments, sorts by relevance, applies strategy. DecompressAsync returns compressed content. Private methods: ExtractKeyPhrases (scores sentences by length/key terms), CondenseToolOutput (finds most informative line), GenerateConversationSummary (extracts core meaning), ExtractToolOutline (command name + status), ExtractUserIntentOutline (question/intent patterns), ExtractAssistantOutline (action descriptions), SplitSentences (smart period handling), SplitByPeriodSafely (preserves decimals/abbreviations), TruncateToLength. Uses global::Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models, OpenLMStudio.Infrastructure namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ConversationEncryption.cs - AES-256-GCM Conversation Encryption
  - 132-line file containing ConversationEncryptionService class and ConversationEncryption static class. ConversationEncryptionService implements IConversationEncryption with Encrypt (IV + Salt + Ciphertext as base64) and Decrypt methods. Uses Aes.Create with 256-bit key, Rfc2898DeriveBytes (PBKDF2, 16-byte salt, 100000 iterations, SHA256). ConversationEncryption static class provides EncryptJson (serializes object to JSON then encrypts) and DecryptJson (deserializes decrypted JSON). Uses System, System.IO, System.Security.Cryptography, System.Text, System.Text.Json, System.Threading, System.Threading.Tasks, Microsoft.Extensions.Logging namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/ConversationManager.cs - File-Based Conversation Persistence
  - 460-line class implementing IConversationManager with file-based JSON persistence. Contains _logger, _storagePath (ApplicationData/OpenLMStudio/chats), JsonOptions (WriteIndented, CamelCase, JsonStringEnumConverter). Methods: CreateChatAsync, LoadChatAsync, DeleteChatAsync, AddMessageAsync (sets Id/CreatedAt, updates MessageCount/TotalTokenCount), GetMessagesAsync (with limit via Skip), SearchChatsAsync (name + content search), SearchMessagesInChatAsync, UpdateChatAsync (reflection-based), CalculateTotalTokenCountAsync, ExportChatAsync, ImportChatAsync, ListChatsAsync (sorted by UpdatedAt desc), GetConversationTokenCount, RenameChatAsync. Private: SaveChatAsync (persists messages with streaming cleared, ToolCalls copied), GetChatFilePath, EstimateTokenCount. Uses System.Collections.Generic, System.Linq, System.Text.Json, System.Text.Json.Serialization, Microsoft.Extensions.Logging, OpenLMStudio.Application.Interfaces, OpenLMStudio.Domain.Models namespaces. Located in OpenLMStudio.Infrastructure.Services namespace.
## src/Infrastructure/Services/Crypto.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DeviceMonitor.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DiffusionInferenceEngine.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DiffusionModelFamilyService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DiffusionModelLoader.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DiffusionPipelineService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DigitalSignatureVerifier.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/DownloadManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/EmbeddingPipelineService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/EngineBinaryDownloader.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/EngineConfigService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/EngineLogger.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/FileOperationsService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/FilePatchTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/FileReadTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/FileWriteTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/GgufChatCompletionLoader.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/GgufModelDownloader.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/GgufParser.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/GitDiffTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/GitHistoryTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/GitRepositoryService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/HardwareDetector.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ImagePostProcessingService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/InteractiveHelpService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/JsonModelRepository.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/KeyboardNavigationService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/KeyboardShortcutsService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/LlamaCppChatCompletionService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/LlamaServerHelpParser.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/LogViewerService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/LoraAdapterManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/LoraWeightMerger.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/MainAIManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/McpClient.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/McpPromptAccessor.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/McpResourceAccessor.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/McpSseClient.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/McpToolCaller.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/MemoryManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ModelCacheCleanupService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ModelLoadingFallbackService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ModelManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ModelMetadataService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ModelRecommendationService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ModelService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/OnboardingService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/OomRecoveryService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/OpenLmStudioLogScope.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/PerformanceBenchmarkService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/PinguSystemPrompts.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/PinguTools.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/PluginRegistry.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/PluginSecurityValidator.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ProjectExplorerTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/RateLimitMiddleware.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SafetensorParser.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SandboxService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SearchFilesTool.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SelfSignedCertificateGenerator.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ServerLoadTestService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ServerService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SqliteTaskRepository.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SseEventBuffer.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SseReconnectService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SyntaxHighlightingMarkdownRenderer.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SystemAIClient.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/SystemAIManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskCompletionDetector.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskContextInheritor.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskContextPruner.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskContextReinjectionService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskContextStore.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskProgressTracker.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskScheduler.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskSchedulerService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TaskValidationService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/TokenEstimator.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/ToolRegistry.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/UpdateManager.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/VaEPipelineService.cs - ???
  - Not Reviewed
## src/Infrastructure/Services/WindowSettingsService.cs - ???
  - Not Reviewed
## src/Infrastructure/StructuredLoggerExtensions.cs - ???
  - Not Reviewed
## src/Infrastructure/SystemAICoordinator.cs - ???
  - Not Reviewed
## src/Infrastructure/ToolchainRegistry.cs - ???
  - Not Reviewed
## src/Infrastructure/VMStore.cs - ???
  - Not Reviewed
