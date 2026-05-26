## src/Domain/Interfaces/IAgentToolExecutor.cs - AgentToolExecutor Interface
  - Orchestrates tool execution for the agent. Routes tool calls to services based on tool name. Defines ExecuteAsync, ListAvailableTools, IsToolAvailable, ExecuteCommandAsync, and CancelRunningCommandAsync methods. Uses ToolResult and ToolDefinition models. (~38 lines)
## src/Domain/Interfaces/IBrowserService.cs - BrowserService Interface
  - Puppeteer browser interaction interface. Single ActionAsync method supporting launch, click, type, scroll_down, scroll_up, and close actions with URL, coordinate, and text parameters. Returns ToolResult with screenshot and console logs. (~19 lines)
## src/Domain/Interfaces/IChatService.cs - ChatService Interface
  - Standard CRUD interface for chat conversations. Defines CreateChatAsync, GetChatAsync, ListChatsAsync, DeleteChatAsync, and AddMessageAsync methods using Chat, Message models with Guid identifiers. (~34 lines)
## src/Domain/Interfaces/ICommandExecutor.cs - CommandExecutor Interface
  - CLI command executor using System.Diagnostics.Process. ExecuteAsync accepts command, requiresApproval flag, optional timeout and workingDirectory. Returns ToolResult. Includes CancelAsync for interrupting long-running commands. (~25 lines)
## src/Domain/Interfaces/IFileSystemService.cs - FileSystemService Interface
  - Comprehensive file system service for agent tools. 5 methods: WriteFileAsync, ReplaceInFileAsync, ReadFileAsync, SearchFilesAsync, ListFilesAsync. All support .agentignore validation via AgentIgnoreRule. Cross-platform with workingDirectory resolution. (~63 lines)
## src/Domain/Interfaces/IMcpService.cs - McpService Interface
  - MCP (Model Context Protocol) service for tool invocation and resource access. 3 methods: UseToolAsync, AccessResourceAsync, LoadDocumentationAsync. All use serverName parameter to target specific MCP servers. Returns ToolResult with text/images. (~33 lines)
## src/Domain/Interfaces/IModelService.cs - ModelService Interface
  - Model management service with 4 CRUD methods: DiscoverModelsAsync (scans repository path), DownloadModelAsync (from remote sources like HuggingFace), SearchModelsAsync (by query), DeleteModelAsync (by Guid). Returns ModelMetadata throughout. (~29 lines)
## src/Domain/Interfaces/IPluginRegistry.cs - PluginRegistry Interface
  - Comprehensive plugin management with IPluginRegistry interface (12 methods) plus PluginSandboxPolicy record, PluginDefinition record, PluginUpdateInfo record, and PluginSandboxPolicyDefaults static class. Covers discovery, install, uninstall, enable/disable, registry URL, and sandbox policies. (~128 lines)
## src/Domain/Interfaces/ISandbox.cs - SandboxService Interface
  - Cross-platform process isolation interface (ISandboxService) with 6 methods using Windows Job Objects and Linux/macOS cgroups v2. Includes SandboxProcessInfo and SandboxResourceUsage records. Supports PluginSandboxPolicy. (~69 lines)
## src/Domain/Interfaces/IWebSearchService.cs - WebSearchService Interface
  - Web search and content fetching service. 2 methods: FetchAsync (fetches URL content, auto-upgrades HTTP to HTTPS) and SearchAsync (with optional allowedDomains/blockedDomains filtering). Returns ToolResult. (~26 lines)
## src/Domain/Models/AgentIgnoreRule.cs - AgentIgnoreRule Model
  - Parses .agentignore patterns with glob support (*, **, ?). Properties: Pattern, IsNegated, MatchesDirectories. Contains Matches(filePath) and WildcardMatch() methods using Regex. No external dependencies beyond System.IO and System.Text.RegularExpressions. (~64 lines)
## src/Domain/Models/AgentSession.cs - AgentSession Model
  - Tracks agent execution state with IDisposable. Properties: Id, State (AgentState enum), IsActive, ToolCalls, WorkingDirectory, ContextSegments, timestamps. Methods: RecordToolCall, UpdateState, AddContext, Dispose. References AgentState, AgentToolCallRecord, ContextSegment from other files. (~71 lines)
## src/Domain/Models/AiAnalysisResult.cs - AiAnalysisResult Model
  - Stores AI analysis context for Git Diff Review (Phase 7.6.1). Properties: AnalyzedChatHistory, ProjectStateAtTimeOfAnalysis, RelevantContextSegmentIds, AnalysisTokenCount, AnalyzedAt. Implements IDisposable (empty). Used by TaskContextSnapshot.AiAnalysis field. (~31 lines)
## src/Domain/Models/BinaryInfo.cs - BinaryInfo Model
  - BackendType enum (Cpu, Cuda, Metal, Vulkan) plus BinaryInfo, GgufModelInfo, ModelRecommendation records and RecommendedSettings class. Contains engine binary, GGUF model, and smart recommendation metadata with properties like Backend, FilePath, Architecture, Quantization, ContextLength. (~84 lines)
## src/Domain/Models/ChatModels.cs - Chat Models
   - Large file (512+ lines) containing MessageRole enum, ToolCall record (with `[JsonPropertyName]` attributes, nullable `Id`, `ParsedArguments`/`ToArguments`/`ToArgumentsSafe` helpers), Message class (with ToolCalls, ImageOutputs, EmbeddingOutputs), Conversation class, Chat class (with StoragePath, ModelId, SystemPrompt, Temperature, MaxTokens, IsActive, Description, Tags, ImageOutputs, EmbeddingOutputs, Messages; legacy ConversationFolderId/ModelIdentifier aliases), ContextSegment class, CompressionLevel/ContextInjectionType enums, ContextWindow class, ContextPruneStrategy enum, ContextBudget class, ConversationEncryption static class (AES-256 with HMAC). References ImageOutput from other files. (~512 lines)
## src/Domain/Models/CompressedEntry.cs - CompressedEntry Models
  - LogLevel and EngineType enums, CompressedEntry class (Summary, KeyDecisions, FilesModified, Timestamp), CompressedStats class (TotalMessages, ActiveWindowSize, CompressedEntriesCount, token estimates, compression ratio). Used for context compression tracking. (~86 lines)
## src/Domain/Models/Device.cs - Device Model
  - Hardware device record (GPU/CPU) with static CreateGpu/CreateCpu factory methods. Properties: Id, Name, Type, Vendor, MemoryBytes, UsedMemoryBytes, IsAvailable, UtilizationPercent, Capabilities. Includes AvailableMemory computed property and SetAvailableMemoryOverride for CPU devices. (~60 lines)
## src/Domain/Models/DeviceInfo.cs - DeviceInfo Models
  - GpuDevice record (ID, Name, Vendor, VRAM, CUDA/Rocm support), CpuInfo record (Model, core counts, RAM, AVX support), DeviceInfo record (CPU + GPUs with HasGpu/TotalVramBytes). Contains factory methods for all three. (~192 lines)
## src/Domain/Models/ImageOutput.cs - ImageOutput Model
  - Image generation output from diffusion pipeline. Properties: Id, ImageData (base64 PNG), MimeType, Width, Height, Seed, CfgScale, Steps, ModelId, GeneratedAt, Prompt, NegativePrompt. (~68 lines)
## src/Domain/Models/ModelLoadState.cs - ModelLoadState Model
  - ModelLoadState enum (Unloaded, Loading, Loaded, Unloading) plus LoadedModelInstance class (with IDisposable). LoadedModelInstance has ModelId, Metadata, GpuMemoryUsageMB, TotalTokensProcessed, LoadedAt, GpuContextHandle, InferenceContextHandle. (~74 lines)
## src/Domain/Models/ModelMetadata.cs - ModelMetadata Models
  - ModelMetadata class (GGUF text generation with 15+ properties: Id, Name, Architecture, Quantization, ContextLength, etc.) and MultiModalModelMetadata class (safetensors with LoRA adapter support: LoraFormatVariant, Rank, TensorShapes, TensorDtypes, hashes, CompatibleBaseModel). Both implement IDisposable. (~298 lines)
## src/Domain/Models/ModelType.cs - ModelType Enums
  - ModelType enum (TextGeneration, ImageGeneration, Diffusion, Vae, Lora, Embedding), ModelFormat enum (Gguf, Safetensors, Onnx), LoraFormatVariant enum (LoRa, LoHa, LoKr). (~80 lines)
## src/Domain/Models/PinguState.cs - PinguState Models
   - PinguMood/PinguPanelType/AwakeningPhase enums, PinguAvatarConfig class (bob speed, blink settings), PinguState reactive state machine (Mood, IsVisible, IsMenuOpen, ActivePanel, AwakeningPhase, LoadProgress), PinguStateChangedEventArgs. (~100 lines)
## src/Domain/Models/PinguTaskType.cs - PinguTaskType Enum
   - PinguTaskType enum with 9 task types: TaskOrchestration, UIControl, ModelManagement, GamePlay, Wandering, UserAssistant, ModelRun, Workflow. (~47 lines)
## src/Domain/Models/PinguMesh.cs - Pingu Mesh Data Models
   - PinguVertex (position, normal, UV, 4-bone skin indices/weights), PinguTriangle (vertex indices + Z-order), PinguMeshData (vertex/triangle arrays, texture atlas bytes, atlas dimensions, count display). (~100 lines)
## src/Domain/Models/PinguCharacterData.cs - Pingu Character Data Model
   - PinguCharacterData record (MeshData, BoneHierarchy, AnimationClips, PhysicsParams, TextureAtlas, Seed) for aggregated character data. Used by PinguMeshGenerator as the complete output of mesh generation. (~30 lines)
## src/Domain/Models/PinguBone.cs - Pingu Bone & Hierarchy Models
   - PinguBone (name, parent, transform, rotation, scale, joint limits, world/local matrices), PinguBoneHierarchy (definitions, resolved bones, root bones, Resolve() method), PinguBoneDefinition (pre-resolution JSON schema). (~140 lines)
## src/Domain/Models/PinguSkinWeights.cs - Pingu Skin Weight Models
   - PinguSkinInfluence (bone index + weight), PinguVertexSkinData (4-slot influence array), PinguSkinWeightEntry (binary mesh format). (~50 lines)
## src/Domain/Models/PinguAnimationClip.cs - Pingu Animation Clip Models
   - AnimationKeyframe (time, position, Euler rotation, scale), BoneAnimationTrack (bone index + keyframes), PinguAnimationClip (name, duration, looping, tracks, keyframe counts). (~80 lines)
## src/Domain/Models/PinguAnimationState.cs - Pingu Animation State Models
   - PinguAnimationState enum (Idle/Walk/Run/Sit/Wave/Scratch/Twitch/EarFlick/HeadTurn/Blink/SittingDown/SittingUp/Playing/Breathing), PinguAnimationStateConfig (clip name, duration, blend speed, interruptible, priority). (~60 lines)
## src/Domain/Models/PinguPhysicsParams.cs - Pingu Physics Parameters
   - Mass, friction, gravity, IK stiffness, velocity damping, spring stiffness/rest length, max walk/run speeds, acceleration/deceleration. (~65 lines)
## src/Domain/Models/PinguNPC.cs - Pingu NPC Character Model
   - PinguRole enum (Idle/Worker/Explorer/Assistant/Guardian/Artist/Chef/Scientist/Wandering), PinguToolType enum (None/Pickaxe/Sledgehammer/PokeStick/Paintbrush/ChefHat/LabCoat/Crown/Hat), PinguTask (id, description, type, priority, status, timestamps), PinguNPCState enum, PinguNPC class (appearance, position, velocity, role, tool, animation, task queue, hat, physics). (~140 lines)
## src/Domain/Models/PinguHomeScene.cs - Pingu Home Scene Models
   - PinguHomeObject (name, type, position, size, color, interactivity, animation), PinguHomeScene (background, dimensions, objects). Static factories: CreateDefaultIgloo, CreateDefaultSink, CreateDefaultRug, CreateDefaultBall, CreateDefaultFishBowl, CreateDefaultNest. (~130 lines)
## src/Domain/Models/PinguHat.cs - Pingu Hat Model
   - PinguHat (name, type, bone attachment, offset, rotation, scale, color, putOn/takeOff animations). (~50 lines)
## src/Domain/Models/PinguTool.cs - Pingu Tool Model
   - PinguTool (name, type, bone attachment, offset, rotation, scale, color, use/equip/unequip animations). (~50 lines)
## src/Domain/Models/QEMUTypes.cs - QEMU Types
  - ArchitectureType/AcceleratorType/DiskFormatType/NetworkBackendType/VMRunState enums, CpuTopology record, DiskImageConfig/NetworkDeviceConfig/QmpSocket records, VMInstance class (with ProcessId string, QEMUProcessManager reference), VMCreationConfig record. (~155 lines)
## src/Domain/Models/SmallModels.cs - Small Models
  - DeviceHardwareInfo record, ModelMemoryEntry record, LogEntry record, VMCreationForm class, SubagentResult class, TaskStatus enum (6 states), TaskPhase enum (5 phases). (~125 lines)
## src/Domain/Models/SystemAIConfig.cs - SystemAIConfig Model
  - Configuration for System AI (llama.cpp) client. Properties: ModelPath, Port (8081), SystemPrompt, Temperature (0.3), TopP (0.9), MemoryLock, RecommendedBackend, GpuLayers (35). (~49 lines)
## src/Domain/Models/TaskModels.cs - Task Models
  - Large file (852 lines). Enums: TaskPriority, AgentState, AgentStateExtended, TaskBranchStatus, CheckpointType. Classes: TaskEntity, TaskBranch, TaskCheckpoint, TaskContextSnapshot (with AiAnalysis), AgentTaskState, AgentTaskProgress, AgentTaskChecklistItem, AgentTaskSettings (with GenerateUlid), AgentAutoApprovalSettings, AgentBrowserSettings, AgentFocusChainSettings. Records: AgenticTask, AgentToolCallRecord. (~852 lines)
## src/Domain/Models/ToolAndRegistryModels.cs - Tool and Registry Models
  - ToolDefinition record, ToolAvailability enum, ToolResult record (with Ok/Fail/WithImages static methods), ToolRegistry class (Register, RegisterRange, GetTool, ListTools, IsToolAvailable, Unregister, Clear), McpServerConfig class with nested McpToolInfo/McpResourceInfo records, BrowserSession class (with Create/Close). (~258 lines)
## src/Domain/OpenLMStudio.Domain.csproj - Domain Project File
  - Minimal .NET 8 SDK-style project file with ImplicitUsings and Nullable enabled. Single package reference: System.Text.Json 8.0.5. Root namespace: OpenLMStudio.Domain. (~14 lines)
