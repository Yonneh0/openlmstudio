## src/Domain/Interfaces/IAgentToolExecutor.cs - 38 lines - AgentToolExecutor Interface
  - Orchestrates tool execution for the agent. Routes tool calls to services based on tool name.
## src/Domain/Interfaces/IBrowserService.cs - 19 lines - BrowserService Interface
  - Puppeteer browser interaction interface. Single ActionAsync method supporting 6 actions.
## src/Domain/Interfaces/IChatService.cs - 34 lines - ChatService Interface
  - Standard CRUD interface for chat conversations with Guid identifiers.
## src/Domain/Interfaces/ICommandExecutor.cs - 25 lines - CommandExecutor Interface
  - CLI command executor using System.Diagnostics.Process with cancel support.
## src/Domain/Interfaces/IFileSystemService.cs - 63 lines - FileSystemService Interface
  - Comprehensive file system service for agent tools. 5 methods with .agentignore validation.
## src/Domain/Interfaces/IMcpService.cs - 33 lines - McpService Interface
  - MCP service for tool invocation and resource access. 3 methods with serverName parameter.
## src/Domain/Interfaces/IModelService.cs - 29 lines - ModelService Interface
  - Model management service with 4 CRUD methods for model discovery and download.
## src/Domain/Interfaces/IPluginRegistry.cs - 128 lines - PluginRegistry Interface
  - Comprehensive plugin management with 12 methods covering discovery, install, uninstall, enable/disable, and sandbox policies.
## src/Domain/Interfaces/ISandbox.cs - 69 lines - SandboxService Interface
  - Cross-platform process isolation using Windows Job Objects and Linux/macOS cgroups v2.
## src/Domain/Interfaces/IWebSearchService.cs - 26 lines - WebSearchService Interface
  - Web search and content fetching service with domain filtering support.
## src/Domain/Models/AgentIgnoreRule.cs - 64 lines - AgentIgnoreRule Model
  - Parses .agentignore patterns with glob support (*, **, ?). Matches(filePath) method with regex.
## src/Domain/Models/AgentSession.cs - 71 lines - AgentSession Model
  - Tracks agent execution state with IDisposable. Properties: Id, State, IsActive, ToolCalls, WorkingDirectory.
## src/Domain/Models/AiAnalysisResult.cs - 31 lines - AiAnalysisResult Model
  - Stores AI analysis context for Git Diff Review. Properties: AnalyzedChatHistory, ProjectStateAtTimeOfAnalysis.
## src/Domain/Models/BinaryInfo.cs - 84 lines - BinaryInfo Model
  - BackendType enum plus BinaryInfo, GgufModelInfo, ModelRecommendation records with engine binary metadata.
## src/Domain/Models/ChatModels.cs - 512 lines - Chat Models
  - MessageRole, ToolCall, Message, Conversation, Chat, ContextSegment, CompressionLevel, ContextWindow, ContextBudget, ConversationEncryption.
## src/Domain/Models/CompressedEntry.cs - 86 lines - CompressedEntry Models
  - LogLevel, EngineType enums plus CompressedEntry, CompressedStats classes for context compression tracking.
## src/Domain/Models/Device.cs - 60 lines - Device Model
  - Hardware device record (GPU/CPU) with CreateGpu/CreateCpu factory methods.
## src/Domain/Models/DeviceInfo.cs - 192 lines - DeviceInfo Models
  - GpuDevice, CpuInfo, DeviceInfo records with GPU detection and device capabilities.
## src/Domain/Models/ImageOutput.cs - 68 lines - ImageOutput Model
  - Image generation output from diffusion pipeline with base64 PNG data.
## src/Domain/Models/ModelLoadState.cs - 74 lines - ModelLoadState Model
  - ModelLoadState enum plus LoadedModelInstance class with IDisposable.
## src/Domain/Models/ModelMetadata.cs - 298 lines - ModelMetadata Models
  - ModelMetadata (GGUF text generation) and MultiModalModelMetadata (safetensors with LoRA support).
## src/Domain/Models/ModelType.cs - 80 lines - ModelType Enums
  - ModelType, ModelFormat, LoraFormatVariant enums for model categorization.
## src/Domain/Models/PinguModels.cs - 1100 lines - Consolidated Pingu Models
  - All Pingu model types: Enums (10), Accessories, Mesh, Bones, Animation, Physics, NPC, HomeScene, CharacterData, State.
## src/Domain/Models/PinguToolHolder.cs - 60 lines - Pingu Tool Holder Model
  - PinguToolHolder with ToolAttachment record for managing which bone holds which tool.
## src/Domain/Models/QEMUTypes.cs - 155 lines - QEMU Types
  - ArchitectureType/AcceleratorType/DiskFormatType/NetworkBackendType/VMRunState enums plus VMInstance, VMCreationConfig.
## src/Domain/Models/SmallModels.cs - 125 lines - Small Models
  - DeviceHardwareInfo, ModelMemoryEntry, LogEntry, VMCreationForm, SubagentResult, TaskStatus, TaskPhase.
## src/Domain/Models/SystemAIConfig.cs - 49 lines - SystemAIConfig Model
  - Configuration for System AI (llama.cpp) client with model path, port, temperature, GPU settings.
## src/Domain/Models/TaskModels.cs - 852 lines - Task Models
  - TaskPriority, AgentState, TaskBranchStatus, CheckpointType enums plus TaskEntity, TaskBranch, TaskCheckpoint, TaskContextSnapshot, AgentTaskState, AgenticTask.
## src/Domain/Models/ToolAndRegistryModels.cs - 259 lines - Tool and Registry Models
  - ToolDefinition, ToolResult, ToolAvailabilityRegistry, McpServerConfig, BrowserSession types.
## src/Domain/OpenLMStudio.Domain.csproj - 14 lines - Domain Project File
  - Minimal .NET 8 SDK-style project with System.Text.Json reference.