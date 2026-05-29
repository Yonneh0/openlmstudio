


# OpenLMStudio

A cross-platform desktop application for running and managing local AI models, built with .NET 8 and Avalonia UI. Provides an OpenAI/Anthropic-compatible inference server alongside a chat client with context management, multi-model support, and an agentic task harness.

## Overview

OpenLMStudio is a cross-platform desktop application (.NET 8, Avalonia 12) that combines a local AI inference server with an integrated agentic workspace. At its core is "Pingu," a reactive system-AI avatar (~2600 lines of animation, IK, physics, and behavior logic) that orchestrates task execution through a plan/act cycle with 26+ tools spanning file operations, git, command execution, browser automation, and MCP protocol. The app manages two model ecosystems: text generation via llama.cpp GGUF models and image generation via an ONNX Runtime diffusion pipeline (SD1.5/SDXL/SD3/Flux), both backed by HuggingFace downloads, LoRA adapter support, and VRAM-aware memory management. Context is handled through a SQLite-backed conversation manager with three compression strategies, token budgeting with auto-eviction, and segment pinning/suppression. The UI presents a 3-column dark-themed layout with a Pingu avatar panel, image generation tab, and plugin management, while the embedded Kestrel server exposes OpenAI-compatible chat and image endpoints with SSE streaming, API key auth, and rate limiting. Cross-architecture compilation is supported via QEMU VMs with QMP protocol, and the entire system is extensible through a sandboxed plugin registry with SHA256 manifest validation.

**Detailed Feature List**

**Agent & Tooling**
- Pingu system-AI avatar with real-time animation, IK solver, physics, and behavior triggers (Twitch, HeadTurn, Scratch, EarFlick, Blink, Sitting)
- Plan/Act execution cycle with loop detection (50%+ same action), consecutive failure threshold (3), and auto-commit for Write/Patch tools
- 26+ agent tools: FileRead, FileWrite, FilePatch, SearchFiles, ProjectExplorer, CommandExecute, GitDiff, GitHistory, GitBlame, GitBranches, CodeDefinitionExtractor, BrowserAction, UseSkill, UseSubagents, NewTask, PatchService, and more
- Agent task lifecycle with SQLite-backed priority scheduling, context inheritance (parent→child), context pruning (Archive/Compress/Discard), and fast reinjection (<100ms)
- AI-powered task completion detection via Pingu
- Activity tracing with per-task timing and resource consumption (ConcurrentDictionary)
- Agent session persistence to disk with checkpoint management

**Model Management**
- GGUF model support (llama.cpp) with GGUF parser (v1-v3, 51 tag types, 27 quantization patterns)
- Safetensors model support with SHA256/MD5 hashing
- HuggingFace Hub integration for model downloads
- Diffusion pipeline (ONNX Runtime): SD1.5, SDXL, SD3, Flux.1-dev, Flux.2, Flux.1-schnell
- Text encoding (CLIP/T5), UNet/DiT denoising, VAE encoding/decoding
- LoRA adapter management with weight merging (W_base + alpha/rank * delta_W)
- Dual model system: MainAI (text) + SystemAI (Pingu) with concurrent loading
- VRAM and CPU memory tracking with eviction scoring
- Model loading fallback chain (GPU→CPU→degraded parameters)
- OOM recovery with progressive degradation
- Model cache cleanup (KeepAll/RemoveByLastUsed/EvictBySize/Aggressive)
- Self-signed HTTPS certificate generation (OpenSSL/dotnet dev-certs)

**Context & Conversation**
- SQLite-backed chat context manager with 4 tables: ChatMessages, PinSegmentStates, SuppressSegmentStates, CustomInjections
- Three compression strategies: Light (key phrase extraction), Medium (summary generation), Aggressive (outline-level)
- Context relevance scoring: 30% recency, 40% semantic, 30% entity match
- Token budget management with auto-eviction of lowest-relevance segments
- Context manipulation: Pin, Unpin, SuppressToggle, RemoveFromContext, AddCustomContext
- AES-256-GCM conversation encryption with PBKDF2 key derivation (100K iterations)
- Context compression using System AI (1B CPU model)
- Chat search, export, and encryption

**Task Management**
- SQLite task repository with priority scheduling (Tasks/ToolCalls/Branches tables)
- Task validation using Pingu with word-boundary-aware PASS detection
- Task context snapshots with file-based + ConcurrentDictionary storage
- Branch task caching with abandon/pause/resume
- AI-powered context reinjection from compressed snapshots
- Task hooks: TaskComplete, UserPromptSubmit, ToolCall, StateChange
- Auto-approval of agent tools/commands with 21+ tool mappings

**Desktop UI (Avalonia 12)**
- 3-column layout: 280px left sidebar, 4* center pane, 320px right sidebar
- Dark theme with 30+ embedded styles
- Pingu avatar panel with 6 tabs: Skills, Settings, Models, Compile, Logs, About
- Image generation tab with pipeline selector, mode tabs (Generate/Image→Image/Inpaint/Variation), LoRA, progress bar, preview, gallery
- Chat title editing, agent mode toggle, safety toggles (WWW/Read/Edit/Exec)
- Plugin management window (900x700) with search, cards, policy controls
- Window state persistence (JSON)
- Keyboard navigation and shortcuts (11 default shortcuts)
- Accessibility: high-contrast mode, screen reader support, font size, keyboard navigation
- Markdown rendering with syntax highlighting (Markdig)
- Git log popup with 3-column table (hash/author/message)
- Tool call form with dynamic parameter generation

**Infrastructure & Services**
- Kestrel server with OpenAI-compatible endpoints (chat completions, image generation)
- SSE streaming with event buffering and reconnection replay
- API key authentication (X-Api-Key header or query parameter)
- Rate limiting middleware (sliding window, 60 req/min default, 429 responses)
- Cross-platform process sandboxing (cgroups v2 on Linux/macOS, Job Objects on Windows)
- QEMU VM process manager with QMP protocol for cross-architecture workflows (14 architectures: X86_64, AArch64, RISC_V64, AVR, MIPS, etc.)
- Hardware detection (GPU via WMIC/SKInfo, RAM, platform detection)
- Recommended backend selection (darwin→Metal, NVIDIA→CUDA)
- Web search and content fetching with domain filtering
- Browser service (Puppeteer) with 6 actions: launch, click, type, scroll_down, scroll_up, close
- Real-time log viewer with filtering, searching, colorization
- Performance benchmarking (model load, chat completion, image generation)
- Application update checker (GitHub Releases API, 1-hour cache)

**Plugin System**
- Plugin registry with remote registry URL support
- Plugin discovery, installation, uninstallation, enable/disable
- Sandbox policies per plugin
- Security validation: SHA256 hash verification, manifest integrity, plugin archive verification
- Plugin provenance checking

**Developer/Utility**
- Git repository service (CLI): branches, tags, commits, diff, blame
- Image post-processing: upscaling, HiRes.fix, ControlNet (Canny/Depth/OpenPose), IP-Adapter face embeddings
- Image format conversion (PNG/JPEG/WebP/ICO/BMP/GIF) with SkiaSharp
- Image gallery with SQLite persistence and auto-generated 128x128 thumbnails
- System prompt generation: dynamic agent prompts, Pingu system prompts (TaskOrchestrator, UIControl, ModelManagement, GamePlay, Wandering, UserAssistant)
- Engine configuration persistence (llama.cpp server settings)
- Structured logging with disk rotation
- OpenTelemetry activity source
- Onboarding service with 8-step first-run flow


### Key Features

| Feature | Status |
|---------|--------|
| Local inference server (OpenAI + Anthropic compatible API) | Partial |
| GGUF model loading via llama.cpp | Partial |
| Safetensors model parsing | Partial |
| Chat interface with conversation persistence | Partial |
| Image generation (SD 1.x, SDXL, SD 3.x, Flux) | Stub |
| Agentic task harness (plan/act cycle) | Stub |
| Context window management with compression | Stub |
| Plugin & MCP system | Stub |
| Device monitoring (GPU VRAM, CPU, RAM) | Stub |

> **⚠️ Development Status**: This project is in early active development. Many documented features have interface contracts and infrastructure scaffolding in place, but core inference is not yet wired to actual model execution. The chat completion service returns simulated placeholder responses. Image generation infrastructure is scaffolded but ONNX Runtime inference uses simulated delays.

## Architecture

```
OpenLMStudio/
├── src/
│   ├── Application/        — Application layer: interfaces, DTOs, DI registration
│   ├── Domain/             — Domain layer: models, domain interfaces
│   ├── Infrastructure/     — Infrastructure layer: concrete service implementations
│   └── Desktop/            — UI layer: Avalonia cross-platform desktop app
├── docs/                   — Project documentation
│   ├── avalonia/           — Avalonia UI reference documentation
│   └── *.md                — Feature and API documentation
└── build.ps1 / build.sh    — Build scripts
```

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| **Domain** | Core business logic, domain models (Chat, Message, Device, ModelMetadata) |
| **Application** | Application services, DTOs, use case orchestration, interface contracts |
| **Infrastructure** | Concrete implementations (ServerService, DownloadManager, GgufParser) |
| **Desktop** | UI layer — cross-platform Avalonia application with MainWindow |

## Tech Stack

| Component | Technology |
|-----------|------------|
| Language/Framework | C# / .NET 8 |
| UI Framework | Avalonia UI (cross-platform: Windows/macOS/Linux) |
| HTTP Server | ASP.NET Core Kestrel (Minimal APIs) |
| Text Inference | llama.cpp (via GgufParser + GgufChatCompletionLoader — native bindings pending) |
| Image Generation | ONNX Runtime + DiffusionInferenceEngine (scaffolded) |
| Persistence | JSON file-based storage (chat conversations) |
| Model Formats | GGUF (text), Safetensors (image/diffusion/VAE/LoRA/embedding) |

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Avalonia Developer Tools](https://learn.avaloniaui.io/) (optional, for development)

### Build & Run

```bash
# Build the project
dotnet build src/Desktop/OpenLMStudio.Desktop.csproj

# Run the desktop application
dotnet run --project src/Desktop/OpenLMStudio.Desktop.csproj
```

### Publish (Self-Contained)

```bash
# Windows x64
dotnet publish src/Desktop/OpenLMStudio.Desktop.csproj -c Release -r win-x64 -o publish/win-x64 --self-contained

# macOS ARM64 (Apple Silicon)
dotnet publish src/Desktop/OpenLMStudio.Desktop.csproj -c Release -r osx-arm64 -o publish/osx-arm64 --self-contained

# Linux x64
dotnet publish src/Desktop/OpenLMStudio.Desktop.csproj -c Release -r linux-x64 -o publish/linux-x64 --self-contained
```

## Usage

### Inference Server

The application includes a local HTTP server that exposes OpenAI-compatible and Anthropic-compatible API endpoints:

```bash
# Start the server via the UI (Server tab → Start Server)
# Then call:
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model": "local", "messages": [{"role": "user", "content": "Hello"}]}'
```

> **Note**: The chat completion service currently returns simulated placeholder responses. Real inference requires llama.cpp native bindings integration.

### Chat Interface

1. Launch the application
2. Use the chat input to send messages (targeting "MainAI" or "Pingu")
3. Conversations are persisted as JSON files in the app data directory

### Model Management

- Navigate to the Models tab to manage GGUF and Safetensors models
- Models are discovered from the application's models directory
- Load/unload models via the MainModelSelector and SystemModelSelector controls

## Development Status

| Component | Status | Notes |
|-----------|--------|-------|
| Server API | Partial | OpenAI/Anthropic endpoints scaffolded, SSE streaming infrastructure in place |
| Chat Completions | Stub | Simulated responses, no real llama.cpp integration |
| GGUF Parsing | Partial | GgufParser reads headers, model discovery works |
| Model Download | Partial | GgufModelDownloader scaffolded |
| Image Generation | Stub | ONNX Runtime pipeline scaffolded, Task.Delay simulates inference |
| Chat Persistence | Partial | File-based JSON storage works |
| Context Management | Stub | Budgeter, compressor, relevance engine interfaces defined |
| Agent Harness | Stub | Plan/act cycle skeleton with error recovery |
| Plugin System | Stub | IPluginRegistry interface defined |
| MCP Support | Stub | McpService, McpClient scaffolded |
| Device Monitoring | Stub | IDeviceMonitor interface defined |

**Overall**: Early development — core inference engines not yet functional.

## Project Structure Details

See [docs/INDEX.md](docs/INDEX.md) for a complete file tree with descriptions of every tracked source file.

## Documentation

| Document | Description |
|----------|-------------|
| [docs/INDEX.md](docs/INDEX.md) | Complete file tree with descriptions |
| [docs/DEVELOPMENT_STATUS.md](docs/DEVELOPMENT_STATUS.md) | Exhaustive feature completion status |
| [docs/API_COMPATIBILITY.md](docs/API_COMPATIBILITY.md) | API endpoint compatibility matrix |
| [docs/MODEL_COMPATIBILITY.md](docs/MODEL_COMPATIBILITY.md) | Supported model formats and known models |
| [docs/GGUF_LOADING.md](docs/GGUF_LOADING.md) | GGUF parsing and model loading pipeline |
| [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | User-facing guide |
| [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) | Common issues and fixes |
| [docs/DEVELOPER_PLUGINS.md](docs/DEVELOPER_PLUGINS.md) | Plugin development guide |
| [docs/avalonia/](docs/avalonia/) | Avalonia UI reference documentation |

## License

[To be determined]