# OpenLMStudio

A .NET 8 implementation of LM Studio's local LLM interface and server capabilities — with extended AI model support beyond text generation (images, diffusion, etc.), a robust agentic build harness for autonomous task completion, and an intelligent context management system.

Built as a cross-platform desktop application supporting Windows, macOS, and Linux with native performance on each platform.

## Features

- **Multi-Modal Model Support** — GGUF (text generation), Safetensors (images/diffusion/VAE/LoRA/embeddings)
- **Local Inference Server** — OpenAI-compatible `/v1/chat/completions` endpoint, Anthropic-compatible `/v1/messages` endpoint
- **Image Generation API** — Stable Diffusion 1.x, SDXL, SD 3.x, Flux models via ONNX Runtime + diffusers integration
- **Agentic Task Harness** — Plan/act cycle with extensible tooling system (file operations, code execution, git integration)
- **Intelligent Context Management** — Token-aware context window with compression, relevance scoring, and user-driven manipulation
- **Plugin & MCP System** — Model Context Protocol client/server for third-party integrations

## Architecture

```
OpenLMStudio/
├── src/
│   ├── Application/        — Application layer: interfaces, DTOs, DI registration
│   ├── Domain/             — Domain layer: models and domain interfaces
│   ├── Infrastructure/     — Infrastructure layer: concrete implementations
│   └── Desktop/            — UI layer: Avalonia cross-platform desktop app
├── OpenLMStudio.slnx       — .NET 8 solution file
└── DEVELOPMENT_PLAN.md     — Detailed development plan
```

### Key Architecture Layers (Clean Architecture)

| Layer | Responsibility | Example |
|-------|---------------|---------|
| **Domain** | Core business logic, domain models | Chat, Message, Device hardware info |
| **Application** | Application services, DTOs, use case orchestration | ChatCompletionRequest, StreamingEventHandler |
| **Infrastructure** | Concrete implementations of interfaces (I/O, external APIs) | ServerService, DownloadManager, GgufParser |
| **Desktop** | UI layer — cross-platform Avalonia application | MainWindow, App entry point |

## Tech Stack

| Component | Technology |
|-----------|------------|
| Language/Framework | C# / .NET 8 (cross-platform: Windows/macOS/Linux) |
| UI Framework | Avalonia UI — cross-platform WPF-like framework |
| HTTP Server | ASP.NET Core Minimal APIs via Kestrel |
| Database | SQLite for all persistent data |
| Text Inference Engine | llama-cpp-net — cross-platform GGUF inference |
| Image Generation Engine | ONNX Runtime + diffusers model integration |
| Embedding Engine | ONNX Runtime + safetensors model loader |

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- For building: Visual Studio 2022, Rider, or `dotnet build` from the command line

### Build & Run

```bash
# Clone the repository
git clone https://github.com/Yonneh0/openlmstudio.git
cd openlmstudio

# Restore dependencies and build
dotnet restore
dotnet build

# Run the desktop application
dotnet run --project src/Desktop/OpenLMStudio.Desktop.csproj
```

## API Endpoints

### OpenAI-Compatible (`/v1`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/chat/completions` | POST | Chat completion (text) |
| `/v1/images/generations` | POST | Image generation via diffusion models |
| `/v1/embeddings` | POST | Embedding generation |
| `/v1/models/list` | GET | List available models |

### Anthropic-Compatible (`/v1`)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/messages` | POST | Message endpoint |

## Model Formats Supported

- **GGUF** — Text generation via llama-cpp-net (Q4_0, Q4_1, Q5_0, Q5_1, Q8_0 variants)
- **Safetensors** — Image generation, diffusion checkpoints, VAE, LoRA adapters, embeddings
  - Single-file and multi-file sharded models supported
  - Header integrity validation via SHA256/MD5

## Development Status

| Phase | Scope | % Complete |
|-------|-------|------------|
| 1: Foundation & Architecture | Core setup, context architecture design | ~64% |
| 2: Model Management System | Multi-model support + Safetensors integration | ~56% |
| 3: Inference Engines & Server API | Text + image + embedding engines + server routing | ~15% |
| 4: Chat & Conversation System | Data models, SQLite-backed persistence | ~67% |
| 5: Context Management System | All context services (conversation + agentic) | ~3% |
| 6: UI Implementation — Sub-phases | 5 sub-panels covering all UI needs | 0% |
| 7: Agent Harness | Plan/act, tooling, project tree, Git integration | 0% |
| 8: Plugin & MCP System | Protocol + plugin management | 0% |
| 9: Resilience, Security & Operations | Error recovery, security model | 0% |
| 10: Testing & Release | Comprehensive testing + documentation | 0% |

**Overall Progress**: ~52% complete across all phases

## Contributing

Contributions are welcome! Please read the [DEVELOPMENT_PLAN.md](./DEVELOPMENT_PLAN.md) for project structure, priorities, and current status before getting started.

## License

[To be determined]