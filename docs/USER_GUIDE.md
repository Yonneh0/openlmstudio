# OpenLMStudio User Guide

## Getting Started

OpenLMStudio is a cross-platform desktop application for running local AI models with a server capable of exposing OpenAI- and Anthropic-compatible APIs.

### Launching the Application

```
dotnet run --project src/Desktop/OpenLMStudio.Desktop.csproj
```

### Main Interface

The main window has five tabs across the top:

| Tab | Purpose |
|-----|---------|
| **Chat** | Conversational chat interface with streaming responses |
| **Server** | Start/stop local inference server, view port and API status |
| **Models** | Discover and list available GGUF / safetensors models |
| **Devices** | Hardware status (CPU cores, RAM, GPU detection) |
| **Context** | Active context window, budget tracking, pin/suppress controls |

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | Create new chat |
| `Ctrl+M` | Show Models tab |
| `Ctrl+S` | Toggle server start/stop |
| `Ctrl+K` | Show Context tab |
| `Ctrl+L` | Open Settings |
| `Enter` | Send message (in chat input) |
| `Shift+Enter` | New line in chat input |
| `Ctrl+Enter` | Send message from chat input |

## Chat

### Starting a Conversation

1. Click **New Chat** (or press `Ctrl+N`)
2. Type your message in the input box at the bottom
3. Press Enter or click the Send button

### Streaming Responses

When the local server is running, responses stream token-by-token via SSE (Server-Sent Events). A "⏳ Generating response..." placeholder appears while the stream initializes.

### Per-Message Context Controls

Each message in the chat has two controls in the upper-right:

- **📌 Pin** — Locks the message so it cannot be compressed or evicted during context window management.
- **👁️ Reveal** — Toggle visibility of the message to the AI. Suppressed messages remain in history but are excluded from the active context window.

### Custom Context Injection

The Context tab includes a "Custom Context Injection" panel where you can add:

- **System Prompt** — Custom system-level instructions
- **File Contents** — Inject raw text from project files
- **Raw Context** — Arbitrary text injected as a pinned segment

## Server

### Starting the Local Server

1. Open the **Server** tab
2. Click **Start Server** (the server defaults to port 8080 with HTTPS)
3. The status indicator turns green when running

The server exposes:

- `POST /v1/chat/completions` — OpenAI-compatible text completions (streaming via SSE)
- `POST /v1/images/generations` — Image generation (DiffusionPipelineService)
- `POST /v1/messages` — Anthropic-compatible messages
- `GET  /v1/models/image/list` — Available image generation models
- `GET  /v1/models/embedding/list` — Available embedding models
- `GET  /v1/models/upscaling/list` — Available upscaling models
- `GET  /v1/models/vae/list` — Available VAE models
- `GET  /v1/models/lora/list` — Available LoRA adapters

### HTTPS Certificate

On Windows, the server automatically trusts the self-signed HTTPS certificate using `dotnet dev-certs --trust`. On other platforms, you may need to manually trust the certificate.

## Image Generation

The Image Generation tab provides a full diffusion pipeline workflow:

1. Select a model from the dropdown (auto-discovers Diffusion/VAE models)
2. Enter a prompt (and optional negative prompt)
3. Adjust parameters: resolution, steps, CFG scale, seed
4. Click **Generate Image**

The pipeline runs: **CLIP Text Encoding → UNet Denoising (CFG) → VAE Decoding** end-to-end.

## Context Window

The Context tab shows:

- **Token Budget** — Visual bar with color-coded zones (green > 20% remaining, orange 5-20%, red < 5%)
- **Compression Strategy** — Choose None / Light / Medium / Aggressive per chat
- **Pinned Segments** — Messages that will not be compressed
- **Suppressed Segments** — Messages that are excluded from context

## Settings

### Server Settings
- **Port** — HTTP port for the local server (default: 8080)
- **HTTPS Certificate** — Path to custom certificate, or auto-generate
- **API Key** — Optional API key for endpoint authentication
- **Rate Limit** — Sliding window rate limit (requests per minute)

### Model Settings
- **Default Model** — Primary model for chat completions
- **Model Directory** — Path where models are stored
- **Default Max Tokens** — Max generation tokens (0 = no limit)
- **Context Compression** — Default compression strategy and token budget

### Image Generation Defaults
- **Resolution** — Default resolution (512x512, 768x768, 1024x1024)
- **Steps** — Number of denoising steps (1-100)
- **CFG Scale** — Classifier-free guidance scale (1.0-20.0)

### Agent Settings
- **Max Iterations** — Maximum agent task iterations (default: 50)
- **Auto-Commit Threshold** — File size below which changes are auto-committed (KB)
- **Require Plan Approval** — Enforce plan phase approval before risky operations

### Plugin & MCP Settings
- **Plugin Registry URL** — Remote registry for plugin discovery
- **Auto-Update Check Interval** — Minutes between plugin update checks (0 = disabled)
- **Default Plugin Sandbox Policy** — Strict / Restricted / Full
- **MCP Client Timeout** — Seconds before MCP tool calls timeout
- **Max Tools per MCP Server** — Maximum tools loaded from each MCP server

### Data Privacy
- **Encrypt Conversations at Rest** — AES-256 with HMAC-SHA256 encrypted storage
- **Export Format** — Default conversation export format

## Troubleshooting

### Server Won't Start
- Check that port 8080 is not in use (`netstat -ano | findstr :8080`)
- Try a different port in Server Settings
- Check the application logs in the appdata/logs directory

### No Models Discovered
- Ensure GGUF (.gguf) or safetensors (.safetensors) files are in the model directory
- The repository auto-discovers models on startup and when the Models tab is opened
- Check that model files are not corrupted (SafetensorParser validates headers before loading)

### Streaming Not Working
- The server must be running for streaming responses
- If the server disconnects, the app automatically retries once after a 1-second delay
- Check that the HTTPS certificate is trusted

### GPU Not Detected
- GPU detection depends on the system's Vulkan/NVIDIA drivers
- On Windows, ensure the latest GPU drivers are installed
- The app falls back to CPU inference if no GPU is available