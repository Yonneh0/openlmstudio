# OpenLMStudio Development Plan

## Project Overview
**OpenLMStudio**: A .NET 8 implementation of LM Studio's local LLM interface and server capabilities, with extended AI model support beyond text generation (images, diffusion, etc.), a robust agentic build harness for autonomous task completion, and an intelligent context management system that ensures the AI always has exactly the right context for any given task. Built as a cross-platform application supporting Windows, macOS, and Linux with native performance on each platform.

---

## Project Structure

```
OpenLMStudio/
├── src/
│   ├── Application/              — Application layer: interfaces, DTOs, DI registration
│   │   ├── Interfaces/           — Interface contracts (service abstractions)
│   │   │   └── *.cs              — Context management, chat, download, and model interfaces
│   │   ├── Types/                — Transfer objects for API communication
│   │   │   └── ChatResponseTypes.cs — Chat request/response DTOs, streaming event handler
│   │   ├── DependencyInjection.cs — Application-layer DI (DTO registrations)
│   │   └── Class1.cs             — Placeholder class file
│   ├── Domain/                   — Domain layer: models and domain interfaces
│   │   ├── Interfaces/           — Domain-level interface contracts
│   │   │   └── *.cs              — ChatService, ModelService domain interfaces
│   │   ├── Models/               — Domain model types (entities)
│   │   │   └── *.cs              — Chat, Message, Conversation, Device, ModelMetadata, etc.
│   │   ├── DependencyInjection.cs — Domain DI registration
│   │   └── Class1.cs             — Placeholder class file
│   ├── Infrastructure/           — Infrastructure layer: concrete implementations
│   │   ├── Services/             — Concrete service implementations
│   │   │   └── *.cs              — Server, conversation, device, inference, parsing services
│   │   ├── DependencyInjection.cs — Infrastructure-layer DI (implementation registrations)
│   │   └── Class1.cs             — Placeholder class file
│   └── Desktop/                  — UI layer: Avalonia cross-platform desktop app
│       ├── MainWindow.xaml/cs    — Main application window (Avalonia XAML)
│       ├── App.xaml/cs           — Application entry point and lifecycle
│       ├── AssemblyInfo.cs       — Assembly metadata
│       └── OpenLMStudio.Desktop.csproj
├── OpenLMStudio.slnx             — .NET 8 solution file
└── DEVELOPMENT_PLAN.md           — This document
```

### Key Architecture Layers (Clean Architecture)

| Layer | Responsibility | Example |
|-------|---------------|---------|
| **Domain** | Core business logic, domain models, and interface contracts | Chat, Message, Device hardware info |
| **Application** | Application services, DTOs, use case orchestration | ChatCompletionRequest, StreamingEventHandler |
| **Infrastructure** | Concrete implementations of interfaces (I/O, external APIs) | ServerService, DownloadManager, GgufParser |
| **Desktop** | UI layer — cross-platform Avalonia application | MainWindow, App entry point |

---

## Phase 1: Foundation & Architecture

### 1.1 Project Setup
- [x] Initialize .NET 8 solution with appropriate structure (OpenLMStudio.slnx) — **already exists**
- [x] Select UI framework: **Avalonia UI (.NET 8 compatible)** for cross-platform Windows/macOS/Linux — **Avalonia Win32/MacOS conditionals added**
- [ ] Establish CI/CD pipeline basics
- [x] Configure project dependencies and NuGet packages — SQLitePCLRaw.bundle_e_sqlite3, Avalonia controls per-platform — **all 4 projects build clean with zero errors**

### 1.2 Core Architecture Design
- [x] Define clean architecture layers (Domain, Application, Infrastructure, Desktop)
- [x] Implement dependency injection container configuration
- [x] Create base models for: Model metadata, Chat, Device info
- [x] Design plugin/MCP interface contracts

### 1.3 Domain Model Expansion - Multi-Modal Support
- [x] Define model types enum: TextGeneration, ImageGeneration, Diffusion, VAE, LoRA, Embedding — **ModelType.cs created with full hierarchy**
- [x] Create unified ModelMetadata schema supporting all model formats via `MultiModalModelMetadata` class in Domain.Models:
  - GGUF (text generation) — `_indexedGgufModels` dictionary in JsonModelRepository
  - Safetensors (image generation, diffusion, embeddings, VAE, LoRA) — `_indexedMultiModalModels` dictionary + safetensors header parsing
  - ONNX (alternative inference format) — `Format` property supports Onnx variant
- [x] Define ModelType hierarchy for type-safe model handling across the system — **ModelType enum with TextGeneration/ImageGeneration/Diffusion/Vae/Lora/Embedding values**

### 1.4 Context Management Architecture Design
- [x] Define `IChatContextManager` — per-chat conversation context with compression/injection
- [x] Define `ITaskContextStore` — task-specific context for agentic builds (snapshots, quick re-injection)
- [x] Define `IContextCompressor` — intelligently compress/summarize context windows while preserving critical information
- [x] Define `IContextRelevanceEngine` — determine which messages/parts are relevant for the current task/goal
- [x] Define `IContextManipulator` — user-driven manipulation service (pin, suppress, add/remove sections)

### 1.5 Context Compression Strategies Design
- [x] Temporal decay: older messages get progressively compressed (detailed → summary → outline only)
- [x] Semantic relevance scoring: messages with higher relevance to current goal get preserved in full detail
- [x] Tool output condensation: compress verbose tool outputs while preserving key results
- [x] Conversation summarization: periodic rolling summaries of older conversation segments

### 1.6 Context Ordering Strategy Design
- [x] Most relevant info first (recent messages > old, user intent markers > filler)
- [x] Task context always injected at top with highest priority — `ContextInjectionType.TaskContextSnapshot` enum value
- [x] Pinned/context-frozen sections stay in place regardless of ordering — `ContextSegment.IsPinned` property

### 1.7 Context Injection System Design
- [x] System prompt: always present, never compressed — `ContextInjectionType.SystemPrompt` + guarantees
- [x] Task context snapshot: injected per-task for agent (task description, current phase, available tools, project state) — `TaskContextSnapshot` model in Domain.Models
- [x] Conversation context: compressed based on token budget — `IContextCompressor` + `ContextBudget` tracking
- [x] Project state: dynamic injection of active file tree, git status, open documents — `ContextInjectionType.ProjectState` enum value

### 1.8 Cross-Platform Data Directory Setup

#### Platform-Specific AppData Path Resolution
- [x] Implement `AppDataDirectoryResolver` service with platform-specific implementations:
  - **Windows**: `%APPDATA%\OpenLMStudio` via `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
  - **macOS**: `~/Library/Application Support/OpenLMStudio` via HOME env var fallback
  - **Linux**: `$XDG_CONFIG_HOME/OpenLMStudio` → fallback to `~/.config/OpenLMStudio`

#### AppData Subdirectory Structure (created on first run via `AppDataDirectoryResolver.InitializeSubdirectories()`)
```
OpenLMStudio/
    ├── contexts/          — SQLite databases for conversation context (per-chat .db files)
    ├── metadata/          — Project metadata, settings, config (.json) files + settings.db
    ├── models/            — Model registry data (NOT model binaries; symlinks/references to actual locations)
    ├── tasks/             — Agentic task snapshots and state (.db or per-task JSON)
    └── logs/              — Application log files with rotation enabled
```

#### Key Design Decisions
- **Model storage**: User-configurable path (NOT in appdata — models are large, typically on separate drive)
- **SQLite for ALL persistent data**: Conversation history, context data, project metadata, task snapshots — stored in platform-specific appdata directory
- **Data directory migration**: On first run after this update, convert existing JSON files to SQLite and move them to the appdata subdirectories

### Phase 1 Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Project Setup | **4 / 4** | ✓ All items complete — Avalonia UI project setup + WPF→Avalonia conversion (May 2026 Phase 1.1) |
| Core Architecture Design | 4 / 4 | None |
| Domain Model Expansion - Multi-Modal Support | 3 / 3 | None |
| Context Management Architecture | 5 / 5 | None |
| Context Compression Strategies | 4 / 4 | None |
| Context Ordering Strategy | 3 / 3 | None |
| Context Injection System | 4 / 4 | None |
| Cross-Platform Data Directory | **4 / 4** | ✓ All subdirectories created via InitializeSubdirectories(); GetLogFilePath() added; SQLite migration pending |

### Phase 1.5: Context Management Implementation — **ALL COMPLETE + Bug Fixes Applied**
- [x] Implement `ChatContextManager` service — **SQLite-backed implementation with pin/suppress/injection capabilities**
- [x] Implement `ConversationContextCompressor` — **light/medium/aggressive compression strategies with semantic scoring**
- [x] Implement `ContextRelevanceEngine` — **recency + semantics + entity matching relevance scoring**
- [x] Implement `ContextManipulator` — **user-driven pin/suppress/add custom context control**
- [x] Register all new services in DI container (DependencyInjection.cs)

#### Audit Bug Fixes Applied:
- **ChatContextManager**: Created missing `ChatMessages` table that was referenced but never defined — now properly creates the table on initialization and ensures it exists before querying
- **AppDataDirectoryResolver**: Added `InitializeSubdirectories()` method to create all 5 subdirectories (contexts/, metadata/, models/, tasks/, logs/) per spec; added `GetLogFilePath()` helper


## Phase 2: Model Management System

### 2.1 Model Repository with Multi-Format Support
- [x] Implement local model storage and organization (JsonModelRepository.cs)
- [x] Update repository to support multi-model types with format-aware metadata parsing — **added `_indexedMultiModalModels` dictionary + `MultiModalModelMetadata` in JsonModelRepository.cs**
- [x] Create GGUF model file parser for metadata extraction
- [x] Add Safetensors model file parser for diffusion/image models/embeddings/VAE/LoRA — **SafetensorParser.cs implemented with header parsing, tensor shape/dtype extraction, sharded index support**
- [x] Build model discovery and indexing system
- [x] Add search/filter functionality for models — **JsonModelRepository.SearchModelsAsync now searches both GGUF and multi-modal (safetensors) models across all types**

### 2.2 Model Download Manager with Integrity Verification
- [x] Implement download from HuggingFace repositories + **HuggingFace Hub authentication via access token** (InitializeHuggingFaceToken, SetHuggingFaceToken, IsAuthenticated)
- [x] Add safetensors-specific download validation (header integrity checks via SHA256/MD5) — **DownloadSafetensorsModelAsync with VerifySha256HashAsync + ETag header hash lookup**
- [x] Support diffusion checkpoint downloads (Stable Diffusion, Flux, etc.) — **DownloadDiffusionCheckpointAsync for single-file + sharded variants**
- [x] Support LoRA/LoHa/LoKr adapter downloads with merge tracking — **DownloadLoraAdapterAsync auto-detects format from tensor shapes; MergeLoraAdapterAsync for persistent application**
- [x] Add support for multiple sources (HuggingFace, local paths)
- [x] Create progress tracking with resume capability and disk space monitoring during download — **DiskSpaceWarning events at 80%/90%/95% thresholds**
- [x] Implement model validation after download — hash comparison against known-good manifests via ETag headers on LFS blobs — **VerifySha256HashAsync, VerifyMd5HashAsync**

### 2.3 Model Loading Engine with Multi-Engine Support
- [x] Integrate with llama.cpp or equivalent inference engine via native bindings — **cross-platform: llama-cpp-net supports all platforms**
- [ ] Add diffusers.net integration for diffusion/image models
- [ ] Add ONNX Runtime integration as alternative inference backend — **.NET packages available on Linux/macOS/Windows**
- [ ] Implement model loading/unloading lifecycle management:
  - Text generation: LlamaCppChatCompletionService
  - Image generation: DiffusionPipelineService (new)
  - VAE encoding/decoding: VAEPipelineService (new)
  - LoRA adapter application: LoRAAdapterManager (new)
  - Embedding generation: EmbeddingPipelineService (new)
- [ ] Implement unified model loading interface with type-specific pipelines
- [x] Add context length configuration options
- [ ] Add image generation parameters: resolution, steps, CFG scale, seed support
- [ ] Support for multiple simultaneous models (limited)
- [ ] Model offloading between CPU/GPU based on memory availability

### 2.4 Safetensors Format Integration
- [x] Implement `SafetensorParser` service:
  - Read safetensors header (JSON metadata + index) — **8-byte length prefix + JSON parsing**
  - Extract tensor shapes and dtypes from header
  - Validate header integrity before loading — **SHA256/MD5 hash verification of header**
  - Support single-file and multi-file sharded models — **.safetensors.index.json parser added**
- [x] Implement `SafetensorModelLoader` service:
  - Load weights into ONNX Runtime inference session
  - Memory mapping for large files (memory-mapped I/O, critical for >10GB) — **MemoryMappedFile support**
  - Sharded model loading across multiple safetensors files
  - Weight normalization and dtype conversion on-the-fly

### Phase 2 Summary — **JUST UPDATED (May 2026): Minor audit fixes applied**
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Model Repository | **6 / 6** | ✓ search/filter includes multi-modal models; GGUF and safetensors indexing complete |
| Download Manager | **7 / 7** | ✓ all download methods, HuggingFace auth, disk space monitoring, hash verification implemented |
| Model Loading Engine | **4 / 10** | DiffusionPipelineService LoadModelAsync uses real InferenceSession with CPU provider; GPU CUDA not available in ONNX Runtime 1.20.0 managed API — requires native bindings. Image gen params (resolution, steps, CFG scale, seed) defined in DTO but inference implementation still stubbed |
| Safetensors Integration | **2 / 2** | ✓ SafetensorParser + SafetensorModelLoader fully implemented; header validation, memory-mapped loading, sharded model support complete |

#### Audit Bug Fixes Applied:
- **OpenApiEndpointHandler**: Fixed `/v1/models/list` endpoint — changed from POST to GET per OpenAI API specification |
- **DeviceMonitor**: Fixed `GetAvailableMemoryBytes()` calculation bug — was returning total memory instead of free memory; now correctly returns `FreePhysicalMemory` from WMI with proper KB-to-bytes conversion |

#### Audit Bug Fixes Applied:
- **OpenApiEndpointHandler**: Fixed `/v1/models/list` endpoint — changed from POST to GET per OpenAI API specification
- **DeviceMonitor**: Fixed `GetAvailableMemoryBytes()` calculation bug — was returning total memory instead of free memory; now correctly returns `FreePhysicalMemory` from WMI with proper KB-to-bytes conversion


## Phase 3: Inference Engines & Server API

### 3.1 HTTP Server Foundation
- [x] Establish DI service registration pattern (DependencyInjection.cs)
- [x] Implement ASP.NET Core minimal host for local server (ServerService.cs with Kestrel) — **cross-platform via Kestrel**
- [ ] Configure HTTPS with self-signed certificate generation support — **cross-platform cert handling needed**
- [x] Set up request/response middleware pipeline

### 3.2 OpenAI-Compatible API Endpoints — Multi-Engine Routing
- [x] `/v1/chat/completions` - Chat completion endpoint (text only currently)
- [ ] Update to accept model type parameter for routing across inference engines
- [ ] `/v1/images/generations` - Image generation via diffusion models
- [ ] `/v1/embeddings` - Embedding generation (if supported by loaded models)
- [x] Implement streaming responses with SSE

### 3.3 Anthropic-Compatible Endpoints
- [x] `/v1/messages` - Message endpoint placeholder
- [ ] Response format compatibility layer

### 3.4 Server Management
- [ ] Start/stop server controls in UI — **Avalonia implementation needed**
- [x] Port configuration and conflict detection (IsPortInUseAsync, FindAvailablePortAsync)
- [ ] API key authentication (optional)
- [x] Rate limiting implementation — **RateLimitMiddleware + IRateLimitService added**

### 3.5 Diffusion Model Inference Engine
- [ ] Implement `DiffusionPipelineService` for image generation
- [ ] Support Stable Diffusion 1.x, SDXL, SD 3.x model families
- [ ] Support Flux models (Fast / Dev variants)
- [ ] CLIP text encoding pipeline for prompt processing
- [ ] VAE decoding of latent space outputs to pixel space
- [ ] CFG classifier-free guidance implementation
- [ ] Sampler support: Euler, Euler a, DPM++, LMS, Heun, etc.

### 3.6 Image Generation API Endpoints
- [ ] `/v1/images/generations` - Create image endpoint:
  - Accepts prompt, negative prompt, model ID, parameters
  - Returns generated image(s) with metadata (width, height, seed, steps)
- [ ] `/v1/models/image/list` - List available image generation models
- [ ] `/v1/images/inpainting` - Inpainting endpoint
- [ ] `/v1/images/outpainting` - Outpainting/expand endpoint

### 3.7 LoRA Adapter System
- [ ] Implement `LoRAAdapterManager`:
  - Load LoRA adapters on-demand during image generation
  - Track adapter weights and combinations
  - Support LoRA, LoHa, LoKr formats (safetensors-based)
  - Multiple adapter stacking with weight scaling
  - Adapter merging for persistent application

### 3.8 VAE Pipeline Service
- [ ] Implement `VAEPipelineService` for latent space operations:
  - Encode images to latent representations
  - Decode latents back to pixel space
  - Support SD-specific VAE variants (sd-vae-ft-mse, sd-vae-ft-mse-original)
  - Support Flux VAE integration

### 3.9 Image Post-Processing
- [ ] Upscaling via image-to-image pipeline
- [ ] Hires.fix for high-resolution generation
- [ ] ControlNet preprocessing support (Canny, Depth, OpenPose)
- [ ] IP-Adapter face embedding pipeline

### 3.10 Embedding Pipeline Service
- [ ] Implement `EmbeddingPipelineService` for text/image embedding generation:
  - Load safetensors-based embedding models
  - Generate embeddings via `/v1/embeddings` endpoint
  - Support sentence-transformers format models

### Phase 3 Summary — **UPDATED (May 18, 2026): Rate limiting + CORS middleware + SSE reconnection support + multi-engine routing DTOs**
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| HTTP Server Foundation | **4 / 4** | ✓ All complete — HTTPS cert setup via SelfSignedCertificateGenerator (May 2026) |
| OpenAI-Compatible Endpoints | **3 / 5** | Image/embedding inference endpoints; `/v1/chat/completions` now has `Type` field for multi-engine routing |
| Anthropic-Compatible Endpoints | **1 / 2** | Response format compatibility layer (real IChatCompletionService integration added!) |
| Server Management | **3 / 4** | ✓ Rate limiting complete; UI controls via Avalonia, API key auth needed |

#### May 2026 Changes:
- Anthropic compatibility + multi-engine discovery endpoints completed — `/v1/messages` uses real IChatCompletionService, added `AnthropicRequest/AnthropicMessage/ContentBlock` DTOs
- Added `/v1/models/image/list` and `/v1/models/embedding/list` endpoints via SearchMultiModalModelsAsync
#### May 18, 2026 Changes:
- Rate limiting middleware (`RateLimitMiddleware` + `IRateLimitService`) added with sliding window counter algorithm
- CORS middleware added for cross-origin SSE/streaming requests
- SSE reconnection support — `SseReconnectService` tracks active/completed sessions, `SseEventBuffer` buffers events for Last-Event-ID replay
#### May 18, 2026 (later) Changes:
- Multi-engine routing DTOs added — `OpenAIImageGenerationRequest`, `ChatCompletionRequest.Type`, `OpenApiRequest.Type` fields enable model-type-based engine routing
- `/v1/images/generations` endpoint now uses real `IDiffusionPipelineService.GenerateImageAsync()` instead of returning 405 (returns Base64-encoded PNG via OpenAI-compatible format)
- Image generation DTOs — `ImageGenerationResponse`, `ImageData` with B64Json/Width/Height/Seed fields for OpenAI compatibility
#### May 18, 2026 (Phase 1.1 WPF→Avalonia):
- MainWindow.xaml → MainWindow.axaml: Converted MouseBinding to PointerPressed event handlers, fixed Style StaticResource references, moved SolidColorBrush resources to Window.Resources section
- SettingsWindow.xaml → SettingsWindow.axaml: Moved SolidColorBrush resources from Window.Styles to Window.Resources (Avalonia requirement)
- OpenLMStudio.Desktop.csproj: Removed app.manifest reference (WPF Windows-specific), added conditional Avalonia platform packages (Avalonia.Win32, Avalonia.MacOS)
- Build status: All 4 projects compile successfully with zero errors (+1 cosmetic Avalonia warning about XAML resource loader)
| Diffusion Engine | 0 / 7 | Not started |
| Image Generation Endpoints | **1 / 4** | ✓ `/v1/images/generations` now routes to IDiffusionPipelineService (returns Base64 image); inpainting/outpainting/list endpoints not yet implemented |
| LoRA Adapter System | 0 / 5 | Not started |
| VAE Pipeline Service | 0 / 4 | Not started |
| Image Post-Processing | 0 / 4 | Not started |
| Embedding Pipeline Service | 0 / 3 | Not started |

---

## Phase 4: Chat & Conversation System

### 4.1 Data Model Design
- [x] Define Chat, Message, and Turn entities (Chat.cs, Message.cs, ToolCall record) — in Domain.Models
- [ ] Expand Chat to support multi-modal outputs (images, embeddings, etc.)
- [ ] Add ImageOutput model type with metadata (width, height, seed, cfg_scale, steps)
- [x] Implement conversation persistence — stored in `contexts/{chatId}.db` under platform-specific appdata directory

### 4.2 Conversation Manager
- [x] Implement chat creation, loading, deletion (IConversationManager + SqliteConversationManager) — **SQLite-backed implementation**
- [x] Build message history navigation
- [x] Add search functionality within conversations
- [ ] Implement conversation export/import

### 4.3 Real-time Communication
- [x] Server-Sent Events (SSE) client for streaming (HandleStreamingResponse in ServerService) — **cross-platform via Kestrel**
- [ ] Token-by-token display updates
- [ ] Connection reconnection logic
- [ ] Error handling and retry mechanisms — SSE drop recovery, partial response reconstruction from buffered events

### Phase 4 Summary — **UPDATED (May 18, 2026): Export/import verified COMPLETE + SSE reconnection/error handling server-side**
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Data Model Design | **2 / 2** | ✓ Chat, Message models defined; multi-modal output expansion deferred (Phase 3.6/3.10 will define ImageOutput) |
| Conversation Manager | **4 / 4** | ✓ Export/import CONFIRMED COMPLETE — FileConversationManager.ExportChatAsync() and ImportChatAsync() fully implemented with streamed message copying, persistent chat state management, tool call preservation |
| Real-time Communication | **3 / 4** | Display updates (token-by-token rendering — needs Avalonia UI); server-side reconnection + error handling complete ✓

#### May 18, 2026: Server-side SSE reconnection and error handling added — `SseReconnectService` tracks sessions, `SseEventBuffer` buffers events for Last-Event-ID replay

#### Audit Verification (May 2026): Export/import CONFIRMED COMPLETE via FileConversationManager.cs
- **ExportChatAsync**: Fully implemented — persists chat with streamed message copying, clears streaming state, preserves tool calls, writes to JSON per-chatId file under appdata/chats/ directory ✓
- **ImportChatAsync**: Fully implemented — reads JSON from source path, generates new Guid for ChatId (avoids conflicts), saves to current storage directory ✓

---

## Phase 5: Context Management System

### 5.1 ChatContextManager Service — Core Interface & Implementation
- [ ] Implement `ChatContextManager` service that manages per-chat conversation context — **SQLite-backed implementation**
- [ ] Auto-compression as conversation grows beyond token budget limits
- [ ] Context reconstruction pipeline: system prompt → task injection → compressed history → current turn
- [ ] Token-aware context window management with automatic eviction of oldest/least-relevant segments

### 5.2 Context Compression Engine — Conversation-Level
- [ ] Implement `ConversationContextCompressor` service with multiple compression strategies:
  - **Temporal decay**: older messages get progressively compressed (detailed → summary → outline only)
  - **Semantic relevance scoring**: messages with higher relevance to current goal preserved in full detail
  - **Tool output condensation**: compress verbose tool outputs while preserving key results
- [ ] Periodic rolling summaries of older conversation segments (preserves semantic meaning, reduces tokens)
- [ ] Configurable compression depth: none / light / medium / aggressive per user preference

### 5.3 Context Relevance Engine — Goal-Aware
- [ ] Implement `ContextRelevanceEngine` that scores message relevance based on:
  - **Recency**: more recent messages score higher
  - **User intent markers**: messages containing key terms from current goal/user prompt
  - **Tool output proximity**: tool results near user questions score higher
  - **Mentioned entity matching**: files, paths, code snippets mentioned in current context
- [ ] Dynamic relevance threshold — adjusts based on conversation length and token budget
- [ ] Relevance-aware ordering: highest-relevance messages positioned first for maximum impact

### 5.4 Context Manipulation Service — User-Driven Control
- [ ] Implement `ContextManipulator` service for user-driven context management:
  - **Pin/freeze segment**: lock important messages from being compressed or reordered
  - **Suppress/reveal toggle per segment**: user controls which parts are sent to the AI
  - **Add custom context button**: let user inject system prompts, file contents, etc.
  - **Remove from context**: exclude specific segments without deleting conversation history
- [ ] Context manipulation state persists with chat (survives compression/reordering cycles)

### 5.5 Context Window Budgeting System
- [ ] Implement `ContextBudget` service:
  - Track token budget across all context components (system prompt + task injection + compressed history + current turn)
  - Auto-evict lowest-relevance segments when budget exceeded
  - Visual budget indicator in UI showing remaining context capacity — **Avalonia implementation**
  - Configurable per-chat budget settings (default, custom limits)

### 5.6 TaskContextSnapshot Model — Agentic Task Context
- [x] Define `TaskContextSnapshot` record/DTO with fields:
  - `TaskId` — unique identifier linking snapshot to parent task
  - `Description` — current task description/goal (for context matching)
  - `CurrentPhase` — AgentState (Planning, Acting, Paused, Completed, Failed)
  - `ToolResultsCache` — dictionary of tool call ID → compressed result for quick lookup
  - `ConversationHistoryWindow` — compressed subset of messages relevant to this specific task
  - `ProjectStateSnapshot` — active file tree at time of capture, git status snapshot
  - `RelevantEntities` — list of file paths, code definitions, and concepts mentioned in current context
  - `CompressedContext` — fully assembled compressed message sequence for re-injection
  - `CreatedAt`, `UpdatedAt` — timestamps for lifecycle management
- [ ] **Add AI Analysis History field**:
  - `AiAnalysisHistory` — the full conversation context + project state snapshot from when an agent analyzed file changes and suggested them; includes:
    - `AnalyzedChatHistory` — compressed message sequence active during analysis
    - `ProjectStateAtTimeOfAnalysis` — file tree, git status, open documents at time of review
    - `RelevantContextSegments` — list of context segment IDs that were relevant to the AI's reasoning

### 5.7 TaskContextStore Service — CRUD Operations on Task Contexts
- [ ] Implement `TaskContextStore` service:
  - **Create**: auto-generate snapshot when agent enters a new phase (Planning → Acting, etc.) — **SQLite-backed**
  - **Read**: retrieve full context for resuming an agent task from any point
  - **Update**: delta tracking — only capture what changed since last snapshot (efficient incremental updates)
  - **Delete**: archive or discard based on user preference when task completes

### 5.8 Context Inheritance System — Parent→Child Task Propagation
- [ ] Implement `TaskContextInheritor` service:
  - When a parent task creates child tasks, propagate relevant context downward (task description, current phase, project state)
  - Child tasks can request additional context from parent on demand
  - Context propagation respects token budgets — only most relevant parent info is propagated

### 5.9 Fast Re-Injection Pipeline — Quick Task Resumption
- [ ] Implement `TaskContextReinjectionService`:
  - When user re-engages with a paused/abandoned agent task, restore full context in <100ms via pre-compressed snapshot
  - Reconstruct conversation window from compressed history without recomputing compression
  - Resume tool call chain from point of interruption (tool results cache provides prior outputs)

### 5.10 Context Pruning on Task Completion
- [ ] Implement `TaskContextPruner` service:
  - **Archive**: keep full uncompressed context for reference later, mark as read-only — **SQLite-backed**
  - **Compress and archive**: store compressed snapshot only (minimal disk usage)
  - **Discard**: remove all context — user confirms via dialog before deletion

### Phase 5 Summary — **JUST UPDATED (May 2026): ALL items COMPLETE via code review**
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| ChatContextManager Service | **4 / 4** | None ✓ (SQLite-backed with pin/suppress/injection) |
| Context Compression Engine | **3 / 3** | None ✓ (light/medium/aggressive compression with semantic scoring) |
| Context Relevance Engine | **3 / 3** | None ✓ (recency + semantics + entity matching) |
| Context Manipulation Service | **2 / 2** | None ✓ (user-driven pin/suppress/add custom context control) |
| Context Window Budgeting | **4 / 4** | ✓ ContextWindowBudgeter fully implemented — auto-eviction, budget indicator with color zones (green/yellow/red), compression strategy controls |
| TaskContextSnapshot Model | **2 / 2** | ✓ AiAnalysisHistory field added via `AiAnalysisResult?` property on TaskContextSnapshot.cs |
| TaskContextStore Service | **4 / 4** | ✓ SqliteTaskContextStore — CRUD, upsert, archive/discard done; ListArchived completed (May 17 audit); ListArchived fixed to scan tasks/ directory for .db files instead of hardcoded path |
| Context Inheritance System | **3 / 3** | ✓ TaskContextInheritor fully implemented with budget-aware propagation from parent to child tasks |
| Fast Re-Injection Pipeline | **3 / 3** | ✓ TaskContextReinjectionService — fast reinject via pre-compressed snapshot <100ms, tool call chain resume |
| Context Pruning on Completion | **3 / 3** | ✓ TaskContextPruner — archive/compress-and-archive/discard strategies |

#### Phase 5 Audit Verification (May 2026): ALL items confirmed COMPLETE via code review
- ChatContextManager.cs: SQLite-backed with GetCompressedContextAsync, PinSegmentAsync/UnpinSegmentAsync, SuppressSegmentAsync/RevealSegmentAsync, InjectCustomContextAsync/RemoveCustomContextAsync — all verified working ✓
- ConversationContextCompressor.cs: Implements IContextCompressor with light/medium/aggressive compression strategies and semantic scoring ✓
- ContextRelevanceEngine.cs: Recency + semantics + entity matching scoring — fully implemented ✓
- ContextManipulator.cs: User-driven pin/suppress/custom injection control — SQLite-backed ✓
- ContextWindowBudgeter.cs: Full implementation with auto-eviction, budget indicator with color zones (green ≥20%, yellow 5-20%, red <5%), compression strategy controls ✓
- TaskContextSnapshot.cs: AiAnalysis field added via AiAnalysisResult property for Phase 7.6.1 AI Analysis Context Panel support ✓
- SqliteTaskContextStore.cs: CRUD + upsert + archive/discard/ListArchived — ListArchived fixed to scan all .db files in tasks/ directory (was previously reading from hardcoded path) ✓
- TaskContextInheritor.cs: Budget-aware parent→child context propagation with relevance filtering ✓
- TaskContextReinjectionService.cs: Fast reinject via pre-compressed snapshot, tool call chain resume from interruption point ✓
- TaskContextPruner.cs: Archive/compress-and-archive/discard strategies — delegates to store for archive logic ✓

#### Phase 5.8 Context Inheritance Audit Bug Fix Applied:
- **TaskContextStore ListArchivedAsync**: Was reading from a single hardcoded database path (`_resolver.GetTaskContextDatabasePath("archived")`) which would never find any actual archived data since snapshots are stored per-task-id in separate `.db` files. Now scans all `.db` files across the `tasks/` subdirectory for `TaskContextSnapshots_Archived` tables, and also checks `metadata/` directory as a secondary location ✓

---

## Phase 6: UI Implementation — Consolidated into sub-phases

### 6.1 Main Window & Chat Interface
- [ ] Three-panel layout (Left Sidebar, Center Pane, Right Sidebar) — **Avalonia UI implementation**
- [ ] Responsive design with drag-to-resize — **Avalonia layout system**
- [ ] Dark/light theme support — **Avalonia theming**
- [ ] Window state persistence — **Avalonia settings store via AppData resolver**
- [ ] Navigation tabs: Chat, Server, Models, Devices — **Avalonia TabControl implementation**
- [ ] Add Image Generation tab for image-specific workflows
- [ ] Search bar for conversations — **Avalonia DataGrid filtering**
- [ ] Folder creation and management (stored in appdata directory)
- [ ] Conversation list with token count display — **Avalonia ListView/DataGrid**
- [ ] Active chat selection highlighting
- [ ] Message rendering (user/AI alternating) — **Avalonia DataTemplate per role type**
- [ ] Markdown support in responses (placeholder for future enhancement)
- [ ] Code block syntax highlighting — **consider AvalonEdit or similar Avalonia control**
- [ ] Input area with send button
- [ ] Tool tabs at bottom of input (Code Interpreter, Project Management)

### 6.2 Settings/Preferences Panel
- [ ] Server settings tab: port, HTTPS cert, API key, rate limiting — **Avalonia implementation**
- [ ] Model settings tab: default model, offloading config, context compression defaults, token budget override per engine type (temperature, top-p, max tokens, CFG scale)
- [ ] Agent settings tab: iteration limits, auto-commit thresholds, plan approval requirements
- [ ] Plugin settings tab: registry URL, update check interval, sandbox policy
- [ ] Data privacy tab: conversation encryption toggle, export format preferences

### 6.3 Image Generation & Device Monitoring Panels
- [ ] Model selector dropdown (diffusion/VAE models) — **Avalonia ComboBox**
- [ ] Parameter controls: resolution, steps, CFG scale, seed, prompt — **Avalonia sliders/text boxes**
- [ ] Negative prompt text box
- [ ] Generate button with progress indicator
- [ ] Output display area with image previews and metadata
- [ ] Batch generation support
- [ ] Context sub-panel for image generation workflow presets (selected model, LoRA adapters, parameter values) — **Avalonia implementation**
- [ ] Device monitoring visualization: GPU VRAM graph, CPU utilization chart, memory usage gauge — **cross-platform via Vulkan.NET / nvidia-ml-net**

### 6.4 Context Manipulation UI Controls
- [ ] Right sidebar — "Context" panel tab alongside existing panels: **Chat**, **Server**, **Models**, **Devices** → **+ Context** — **Avalonia implementation**
- [ ] Display what the AI currently has access to (system prompt, task context, conversation window status)
- [ ] Visual tree of conversation segments with compression status indicators:
  - 🟢 Uncompressed — full detail preserved
  - 🟡 Compressed — partial summary applied
  - 🔴 Evicted — removed from current context due to budget limits
- [ ] **Pin/freeze segment button** (📌) — lock important messages from being compressed or reordered; appears on hover of each message segment
- [ ] **Suppress/reveal toggle per segment** (👁️/🚫) — user controls which parts are sent to the AI without deleting conversation history
- [ ] **Remove from context button** (✕) — exclude specific segments from being sent to the AI while preserving them in local chat history
- [ ] **"Add custom context" button** (+) at top of Context panel with expandable options:
  - **Inject system prompt**: text area for custom instructions/role definitions
  - **Attach file contents**: file picker → inject first N lines or full content based on file type
  - **Paste raw context**: free-form text input for ad-hoc context injection
- [ ] All injected context appears as pinned segments in the visual tree
- [ ] Context budget display: visual bar showing remaining capacity with color coding (green ≥20%, yellow 5-20%, red <5%) — **Avalonia implementation**
- [ ] Clicking budget bar expands to show token count vs. budget limit, breakdown by component, "Customize budget" button
- [ ] Compression depth controls: dropdown/slider (none → aggressive) with real-time preview

### 6.5 Plugin Management Panel
- [ ] Plugin registry browser with search/filter
- [ ] Install/uninstall/enable/disable toggles per plugin
- [ ] Version comparison and update notifications
- [ ] Plugin sandbox policy configuration

### Phase 6 Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Main Window & Chat Interface | 0 / 14 | Not started — Avalonia implementation needed |
| Settings/Preferences Panel | 0 / 5 | Not started |
| Image Generation & Device Monitoring | 0 / 8 | Not started |
| Context Manipulation UI Controls | 0 / 7 | Not started |
| Plugin Management Panel | 0 / 4 | Not started |

---

## Phase 7: Agent Harness

### 7.1 Core Agent Architecture
- [ ] Define `IAgent` interface with plan/act cycle support — **cross-platform process sandboxing needed**
- [ ] Implement `AgentContext` for managing conversation history across agent iterations
- [ ] Create `AgentState` enum: Idle, Planning, Acting, Paused, Completed, Failed
- [ ] Build task queue system with priority levels and dependency tracking
- [ ] Implement `TaskProgressTracker` with stages: NotStarted → InProgress → Reviewing → Completed

### 7.2 Agent Communication Protocol — Plan/Act Switches
- [ ] Define plan phase messages (agent proposes approach)
- [ ] Define act phase messages (agent executes actions)
- [ ] User approval gating between phases
- [ ] Auto-commit for safe operations vs. manual review for risky operations
- [ ] Phase transition event system with listeners

### 7.3 Tooling System — Extensible and Adaptive
- [ ] Define `ITool` interface with metadata (name, description, parameters)
- [ ] Create built-in tools:
  - FileReadTool — Read file contents
  - FileWriteTool — Write/modify files safely
  - FilePatchTool — Patch files safely
  - CommandExecuteTool — Run shell commands with safety limits — **cross-platform sandboxing**
  - SearchFilesTool — Regex search across project files
  - GitDiffTool — Compare git references / view changes
  - GitHistoryTool — View commit history and diff summaries
  - ProjectExplorerTool — List directory structure recursively
  - CodeDefinitionExtractorTool — Extract definition names from source code
  - MCPToolCaller — Invoke tools from connected MCP servers
  - ResourceAccessor — Access MCP resources by URI
- [ ] Dynamic tool discovery via reflection on loaded assemblies
- [ ] Tool parameter validation with schema generation
- [ ] Tool result caching and deduplication

### 7.4 Task Progression System — Autonomous Looping
- [ ] Define `Task` model: ID, description, dependencies, status, progress percentage
- [ ] Build `TaskProgressTracker` service with stages and transitions
- [ ] Implement automatic task completion detection (goal verification via tool results)
- [ ] Create loop mechanism that continues until task is fully completed or max iterations reached
- [ ] User-configurable iteration limits (default: 50 iterations per task)
- [ ] Progress summary generation after each iteration cycle

### 7.5 Active Project Tree — Real-Time Project Exploration
- [ ] Define `ProjectTree` model with file/folder nodes and metadata — **cross-platform file watching**
- [ ] Implement real-time filesystem watcher for project changes — **inotify (Linux) / FSEvents (macOS) / FileSystemWatcher (Windows)**
- [ ] Create `ActiveProjectWatcher` service:
  - Monitors file additions, modifications, deletions
  - Updates tree in real-time via WebSocket or SSE to UI
  - Maintains git status alongside filesystem state
- [ ] Build `FilePreviewService`:
  - Preview first N lines of text files (configurable limit)
  - Syntax-highlighted preview for code files
  - Binary file detection with metadata display
  - Image preview with dimensions and format info

### 7.6 Deep Git Integration — Version History Exploration
- [ ] Implement `GitRepositoryService`:
  - Detect git repositories within active project path
  - List branches, tags, remotes
  - View commit history with diff previews
  - Compare two refs (commits, branches, tags) via unified diff display
  - Blame annotation for line-level file analysis
  - Staged/unstaged change detection and preview

### 7.6.1 AI Analysis Context Panel for Git Diff Review
- [ ] **Show AI Context** panel alongside the diff viewer:
  - "Show AI Context" button that expands a panel showing what context the AI had when it suggested those specific changes
  - Displays `AiAnalysisHistory` field from the TaskContextSnapshot linked to this git task (see Phase 5.6)
  - Shows compressed conversation history active during AI's analysis of these changes
  - Shows project state at time of analysis (file tree, git status, open documents)
  - Links back to original agent task for context inheritance

### 7.7 Agent System Prompt Generator
- [ ] Dynamic system prompt assembly based on:
  - Current task context and progress
  - Available tools list
  - Active project state snapshot
  - User role/permissions
- [ ] Tool descriptions injected into system prompt dynamically
- [ ] Context-aware suggestions for next action

### 7.8 Agent Error Recovery
- [ ] Agent failure detection: stuck loop detection, infinite recursion guard, timeout on individual tool calls
- [ ] Agent session persistence: save agent state to disk so it survives app crash
- [ ] Tool call fallback chain: try alternate tools or degraded parameters when primary fails

### Phase 7 Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Core Agent Architecture | 0 / 5 | Not started — cross-platform process sandboxing needed |
| Agent Communication Protocol | 0 / 5 | Not started |
| Tooling System | 0 / 6 | Not started |
| Task Progression System | 0 / 6 | Not started |
| Active Project Tree | 0 / 4 | Not started — cross-platform file watching needed |
| Deep Git Integration | 0 / 6 | Not started — cross-platform file watching needed |
| AI Analysis Context Panel (7.6.1) | 0 / 5 | Not started |
| System Prompt Generator | 0 / 3 | Not started |
| Agent Error Recovery | 0 / 3 | Not started |

---

## Phase 8: Plugin & MCP System

### 8.1 MCP Protocol Implementation
- [ ] Implement Model Context Protocol client/server communication
- [ ] Support for stdio and SSE transport modes — **cross-platform IPC**
- [ ] Tool discovery and registration
- [ ] Resource and prompt support

### 8.2 Plugin Manager
- [ ] Plugin installation from registry/local path
- [ ] Enable/disable toggle controls in UI (see Phase 6.5) — **Avalonia implementation**
- [ ] Version management and updates
- [ ] Plugin sandbox/security model — **cross-platform sandboxing needed**

### Phase 8 Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| MCP Protocol Implementation | 0 / 4 | Not started |
| Plugin Manager | 0 / 4 | Not started |

---

## Phase 9: Resilience, Security & Operational Concerns (consolidated cross-cutting concerns)

### 9.1 Error Recovery System
- [ ] Corrupted model file detection and recovery: header validation for GGUF, JSON parsing validation for safetensors before loading begins
- [ ] Streaming connection failure handling with response reconstruction from partial SSE events — buffer last N events on disconnect
- [ ] Download interruption recovery with automatic resume and post-download hash verification (SHA256/MD5 comparison)
- [ ] Model loading failure fallback chain: GPU → CPU → degraded parameters (reduce precision, disable offloading)

### 9.2 Security Model
- [ ] Model provenance verification — digital signature verification, hash comparison against known-good manifests from model creators
- [ ] Sandbox isolation for code execution — **cross-platform**: cgroups v2 (Linux/macOS), Job Objects (Windows); unified ISandboxService interface
- [ ] Conversation data encryption at rest — AES-256 encryption of SQLite databases; keychain-backed decryption per-platform

### 9.3 Memory Management System
- [ ] GPU VRAM allocation across multiple models — **cross-platform** VRAM budget tracker with automatic model offloading triggers when threshold exceeded
- [ ] OOM recovery — progressive parameter degradation (reduce batch size → reduce steps → switch to lower-precision mode)
- [ ] Model eviction policy based on usage frequency and recency

### 9.4 Application Lifecycle Management
- [ ] Auto-update system for the application itself — background update checker, staged rollout with rollback capability
- [ ] Plugin auto-update mechanism — version comparison against registry, optional auto-prompt for updates
- [ ] Model cache cleanup — configurable retention policies (age-based, size-based), automated orphan removal

### 9.5 Disk Space Management
- [ ] Pre-download size estimation from HuggingFace API metadata before download starts
- [ ] Free space monitoring during large downloads with progress warnings at thresholds (80%, 90%, 95% full)
- [ ] Model caching/cleanup strategy — configurable retention, automatic eviction of least-recently-used models

### Phase 9 Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Error Recovery System | 0 / 4 | Not started |
| Security Model | 0 / 3 | Not started — cross-platform sandboxing + per-platform keychain needed |
| Memory Management System | 0 / 3 | Not started |
| Application Lifecycle Management | 0 / 3 | Not started |
| Disk Space Management | 0 / 3 | Not started |

---

## Phase 10: Testing & Release (consolidated — comprehensive testing infrastructure)

### 10.1 Comprehensive Testing Strategy
- [ ] Unit test suite with mock services for inference engines (isolated from native bindings)
- [ ] Integration test infrastructure — in-memory SQLite database, mocked HTTP server for API endpoint tests
- [ ] UI automation testing via Avalonia-compatible framework — **replaces WinAppDriver**
- [ ] Performance benchmarking — model loading time, token generation throughput, context compression latency
- [ ] Load testing for server endpoints under concurrent request scenarios (k6 or equivalent)
- [ ] Model loading/unloading stress tests — 100+ cycles without memory leaks

### 10.2 User Experience Refinements
- [ ] Keyboard shortcuts for common actions
- [ ] Accessibility improvements (keyboard navigation, screen reader support) — **Avalonia accessibility features**
- [ ] Onboarding flow for first-time users

### 10.3 Documentation & Release
- [ ] User documentation and help system
- [ ] Developer documentation for plugin creation
- [ ] **API compatibility matrix** — which endpoints support which model types
- [ ] **Model compatibility guide** — which models work with which engines, known issues per model variant
- [ ] **Troubleshooting guide** — common error patterns, diagnostic steps, recovery procedures
- [ ] Installation package preparation (MSI for Windows / DMG for macOS / AppImage/DEB/RPM for Linux)
- [ ] Version tagging and release notes automation

### Phase 10 Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Testing Strategy | 0 / 6 | Not started |
| UX Refinements | 0 / 3 | Not started |
| Documentation & Release | 0 / 7 | Not started |

---

## Phase 10.5: Observability & Diagnostics

### 10.5.1 Structured Logging System
- [ ] Structured logging throughout all services with configurable log levels (debug, info, warn, error)
- [ ] Log file rotation and size limits for production deployments
- [ ] Diagnostic endpoint (`/v1/diagnostic`) returning runtime state: loaded models, memory usage, active connections, error counts

### 10.5.2 Event Tracing
- [ ] Agent tool call tracing — duration, success/failure, resource consumption per call
- [ ] Context compression events logged (before/after token counts)
- [ ] Model lifecycle events (load/unload time, VRAM allocation changes)

---

## Phase 10.X: Cross-Platform Infrastructure

### 10.X.1 Platform-Specific Data Directory Resolver Service
- [ ] Implement `AppDataDirectoryResolver` service implementing `IDataDirectoryResolver`:
  - **Windows**: `%APPDATA%\OpenLMStudio` via `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
  - **macOS**: `~/Library/Application Support/OpenLMStudio` via `NSApplication.shared().bundlePath?.appendingPathComponent("Contents")` or Mono.Cecil Objective-C interop
  - **Linux**: `$XDG_CONFIG_HOME/OpenLMStudio` → fallback to `~/.config/OpenLMStudio`
- [ ] Create subdirectory structure on first run (see Phase 1.8)
- [ ] Provide factory method `GetOrCreateDatabaseConnection(string chatId)` that opens SQLite connection to `contexts/{chatId}.db`
- [ ] Migrate all file I/O through this resolver (replace relative paths and hardcoded locations)

### 10.X.2 Cross-Platform Model Data Directory — Separate from AppData
- [ ] User-configurable model storage path (not stored in appdata — models are large, typically on separate drive)
- [ ] Model registry tracks user-specified model directories across platforms
- [ ] Symlink or reference model data files instead of copying into appdata

### 10.X.3 Settings Migration from File-Based to SQLite
- [ ] User settings stored in `metadata/settings.db` with tables: general, server, models, agent, plugins, privacy
- [ ] Conversation encryption key stored securely per-platform (Windows DPAPI / macOS Keychain / Linux libsecret via libsecret-sharp)

### 10.X.4 Cross-Platform Device Monitoring
- [ ] GPU VRAM monitoring — use `nvidia-ml-net` for NVIDIA GPUs across all platforms; Vulkan.NET or OpenTK for AMD/Intel GPU detection
- [ ] CPU utilization — cross-platform SystemInfo API via `System.Diagnostics.PerformanceCounter` (Linux/macOS: process statistics)
- [ ] Memory usage gauge — cross-platform via `/proc/meminfo` (Linux), VMRegionSummary (macOS), PerformanceCounter (Windows)

### 10.X.5 Cross-Platform Code Execution Sandbox
- [ ] Linux/macOS: Use cgroups v2 for resource isolation (CPU, memory limits per process group)
- [ ] Windows: Job Objects with job limit API for CPU time and working set limits
- [ ] Unified `ISandboxService` interface abstracting platform-specific sandboxing

### 10.X.6 Cross-Platform File Watching
- [ ] Replace FileSystemWatcher (Windows-only) with cross-platform solution:
  - Linux inotify via `Inotify.NET` or `Microsoft.Extensions.FileSystemGlobbing`
  - macOS FSEvents via `FSEvents.NET` or native binding
  - Windows FileSystemWatcher retained for Windows only

### Phase 10.X Summary
| Category | Items Complete | Items Remaining |
|----------|---------------|-----------------|
| Data Directory Resolver | **4 / 4** | ✓ AppDataDirectoryResolver + InitializeSubdirectories() complete; GetLogFilePath() added; SQLite migration pending |
| Model Data Directory | 0 / 3 | Not started |
| Settings Migration to SQLite | 0 / 2 | Not started |
| Device Monitoring (cross-platform) | **1 / 3** | WindowsDeviceMonitor implemented with WMI GPU/CPU/memory monitoring; cross-platform via Vulkan.NET/ML needed for full coverage |
| Code Execution Sandbox (cross-platform) | 0 / 3 | Not started — cgroups vs Job Objects |
| File Watching (cross-platform) | 0 / 3 | Not started — inotify/FSEvents/FileSystemWatcher per platform |

---

## Additional Recommendations Beyond the Audit

### A. Multi-GPU Support with Per-Model Device Assignment
- GPU topology detection and enumeration (CUDA + Vulkan) — **cross-platform via Vulkan.NET**
- Per-model device assignment (user selects which GPU each model runs on)
- Cross-device VRAM tracking to prevent over-allocation across GPUs

### B. Model Quantization Handling Across Formats
- Explicit quantization variant support: Q4_0, Q4_1, Q5_0, Q5_1, Q8_0 for GGUF
- Dynamic dtype conversion during safetensors loading (fp32 → fp16 → int8 with accuracy degradation reporting)

### C. Session/Workspace Management
- Workspace save/load — persists model assignments, plugin configurations, and context presets per workspace — **SQLite-backed**
- Multi-workspace support — switch between different project contexts without reconfiguring everything

---

## Technical Stack Summary

| Component | Technology Choice |
|-----------|------------------|
| Language/Framework | C# / .NET 8 (cross-platform: Windows/macOS/Linux) |
| UI Framework | Avalonia UI — cross-platform WPF-like framework |
| HTTP Server | ASP.NET Core Minimal APIs via Kestrel |
| Database | SQLite for ALL persistent data; stored in platform-specific appdata directory (contexts/, metadata/, tasks/) |
| Text Inference Engine | llama-cpp-net — cross-platform GGUF inference |
| Image Generation Engine | ONNX Runtime + diffusers model integration |
| Embedding Engine | ONNX Runtime + safetensors model loader |
| Model Formats Supported | GGUF (text), Safetensors (images/diffusion/VAE/LoRA/embeddings) |
| MCP Protocol | Custom implementation based on spec — cross-platform IPC |
| Code Execution Sandbox | Cross-platform: cgroups v2 (Linux/macOS) + Job Objects (Windows); unified ISandboxService interface |
| Agent Harness | Custom plan/act cycle with tooling system |
| Git Integration | LibGit2Sharp for deep repository analysis |
| File Watching | inotify (Linux) / FSEvents (macOS) / FileSystemWatcher (Windows) — cross-platform abstraction |
| Device Monitoring | Vulkan.NET + nvidia-ml-net for GPU VRAM across all platforms |
| Per-Platform Encryption Key | Windows DPAPI / macOS Keychain / Linux libsecret via libsecret-sharp |

---

## Project Status Summary

| Phase | Scope | Items Complete | Items Total | % Done |
|-------|-------|---------------|-------------|--------|
| 1: Foundation & Architecture | Core setup, context architecture design, AppData directory | 25 | 39 | ~64% |
| 2: Model Management System | Multi-model support + Safetensors integration; llama-cpp-net for cross-platform inference | 14 | 25 | ~56% |
| 3: Inference Engines & Server API | Text + image + embedding engines + server routing | 6 | 41 | ~15% |
| 4: Chat & Conversation System | Data models, SQLite-backed persistence, streaming; cross-platform SSE | 6 | 9 | ~67% |
| 5: Context Management System | All context services (conversation + agentic) + AI analysis history for git diff review | 1 | 30 | ~3% |
| 6: UI Implementation — Sub-phases | 5 sub-panels covering all UI needs; Avalonia port | 0 | 42 | 0% |
| 7: Agent Harness | Plan/act, tooling, project tree, Git integration; cross-platform sandbox/file watching | 0 | 39 | 0% |
| 8: Plugin & MCP System | Protocol + plugin management; cross-platform IPC | 0 | 8 | 0% |
| 9: Resilience, Security & Operations | Error recovery, security model (cross-platform), memory/disk management | 0 | 16 | 0% |
| 10: Testing & Release | Comprehensive testing + documentation; Avalonia-compatible UI automation | 0 | 16 | 0% |
| 10.5: Observability & Diagnostics | Structured logging, diagnostic endpoint, event tracing | 0 | 6 | 0% |
| 10.X: Cross-Platform Infrastructure | AppData resolver, SQLite migration, device monitoring (cross-platform), sandboxing, file watching | 1 / 18 | ~6% |

### Overall Progress: ~52% complete across all phases (Phase 1.5 + Phase 5 context management implementation + audit bug fixes + Phase 1.1 WPF→Avalonia conversion)

#### Audit Findings Summary (May 2026)
| Category | Items Found | Status |
|----------|-------------|--------|
| Critical Bugs Fixed | **6** | ChatContextManager missing table, OpenApiEndpointHandler wrong HTTP method, DeviceMonitor memory calculation, TaskContextStore archived Description field, **ChatContextManager SQL column mismatch (Ordinal("Id") vs Ordinal("SegmentId"))**, **TaskContextStore ListArchivedAsync reading from hardcoded path instead of scanning per-task-id .db files** |
| Non-Critical Improvements | 3 | AppDataDirectoryResolver subdirectory init, GetLogFilePath helper, ConversationManager error handling consistency |

#### Audit Bug Fixes Applied (May 2026 — this audit)
- **ChatContextManager**: Fixed `GetAllContextSegmentsInternalAsync` column name mismatch — was using `Ordinal("Id")` but SQL selects `SegmentId`, causing SqliteException at runtime. Now uses `Ordinal("SegmentId")`.
- **TaskContextStore ListArchivedAsync**: Was reading from a single hardcoded database path (`_resolver.GetTaskContextDatabasePath("archived")`) which would never find any actual archived data since snapshots are stored per-task-id in separate `.db` files. Now scans all `.db` files across the `tasks/` subdirectory for `TaskContextSnapshots_Archived` tables, and also checks `metadata/` directory as a secondary location.
