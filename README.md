# OpenLMStudio

A cross-platform desktop application for running and managing local AI models, built with .NET 8 and Avalonia UI. Provides an OpenAI/Anthropic-compatible inference server alongside a chat client with context management, multi-model support, and an agentic task harness.

## Overview

OpenLMStudio brings LM Studio's local LLM interface and server capabilities to the .NET ecosystem, with extended support for multi-modal models beyond text generation — including image generation, diffusion models, VAE, LoRA adapters, and embeddings.

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