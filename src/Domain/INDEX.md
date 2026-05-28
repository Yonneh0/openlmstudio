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
## src/Domain/Models/AgentSession.cs - 71 lines - AgentSession Model
   - Tracks agent execution state with IDisposable. Properties: Id, State, IsActive, ToolCalls, WorkingDirectory.
## src/Domain/Models/ChatModels.cs - ~1050 lines - Chat Models (consolidated)
   - MessageRole, ToolCall, Message, Conversation, Chat, ContextSegment, CompressionLevel, ContextWindow, ContextBudget, ConversationEncryption, ModelType, ModelFormat, LoraFormatVariant, ModelLoadState, LoadedModelInstance, ImageOutput, CompressedEntry, LogLevel, EngineType.
## src/Domain/Models/DeviceInfo.cs - ~320 lines - DeviceInfo Models (consolidated)
   - GpuDevice, CpuInfo, DeviceInfo, Device records with GPU detection, device capabilities, and hardware device record.
## src/Domain/Models/PinguModels.cs - ~1350 lines - Consolidated Pingu Models (consolidated)
   - All Pingu model types: Enums (10), Accessories, Mesh, Bones, Animation, Physics, NPC, HomeScene, CharacterData, State, PinguAutomation, ModelLifecycleTracer.
## src/Domain/Models/PinguToolHolder.cs - 60 lines - Pingu Tool Holder Model
   - PinguToolHolder with ToolAttachment record for managing which bone holds which tool.
## src/Domain/Models/QEMUTypes.cs - 155 lines - QEMU Types
   - ArchitectureType/AcceleratorType/DiskFormatType/NetworkBackendType/VMRunState enums plus VMInstance, VMCreationConfig.
## src/Domain/Models/SmallModels.cs - ~600 lines - Small Models (consolidated)
   - DeviceHardwareInfo, ModelMemoryEntry, LogEntry, VMCreationForm, SubagentResult, TaskStatus, TaskPhase, AgentIgnoreRule, BackendType, BinaryInfo, GgufModelInfo, ModelRecommendation, RecommendedSettings, SystemAIConfig, TokenEstimator.
## src/Domain/Models/TaskModels.cs - ~950 lines - Task Models (consolidated)
   - TaskPriority, AgentState, TaskBranchStatus, CheckpointType enums plus TaskEntity, TaskBranch, TaskCheckpoint, TaskContextSnapshot, AgentTaskState, AgenticTask, AiAnalysisResult.
## src/Domain/Models/ToolAndRegistryModels.cs - 259 lines - Tool and Registry Models
   - ToolDefinition, ToolResult, ToolAvailabilityRegistry, McpServerConfig, BrowserSession types.
## src/Domain/OpenLMStudio.Domain.csproj - 14 lines - Domain Project File
   - Minimal .NET 8 SDK-style project with System.Text.Json reference.
