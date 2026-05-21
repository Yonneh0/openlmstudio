# OpenLMStudio User Guide

## Getting Started

### Installation

1. Download the appropriate build for your platform from [releases](#)
2. Extract the archive
3. Run the application:
   - **Windows**: Double-click `OpenLMStudio.exe`
   - **macOS**: `open OpenLMStudio`
   - **Linux**: `./OpenLMStudio`

### First Launch

When you first launch OpenLMStudio, you'll see the main window with:

- **Left Sidebar** — Navigation tabs (Chats, Server, Models, Devices, Context, Image Gen)
- **Center Pane** — Chat interface with message display and input
- **Right Sidebar** — Context panel with budget indicator and controls

## Interface Overview

### Navigation Tabs

#### Chat Tab
- Create new chats with the **+ New Chat** button
- Browse conversations in the chat list
- Click a chat to load its message history
- Hover over a chat to see its token count

#### Server Tab
- Start/stop the local inference server
- View the server port and status
- Configure server settings in the Settings dialog

#### Models Tab
- Browse available GGUF and Safetensor models
- Add models from your local file system
- Load/unload models for inference

#### Devices Tab
- View GPU VRAM, CPU cores, and available RAM
- See currently loaded model information
- Monitor device resources in real-time

#### Context Tab
- View the context budget (green = healthy, yellow = warning, red = critical)
- Select compression strategies
- Add custom context injections
- Pin/suppress conversation segments for fine-grained control

#### Image Gen Tab
- Select a diffusion model (SD 1.x, SDXL, SD 3, Flux)
- Enter a prompt and optional negative prompt
- Configure resolution, steps, CFG scale, and seed
- Generate images with streaming progress updates
- Apply LoRA adapters for style transfer

### Settings Dialog

Access Settings via the gear icon in the left sidebar. Available tabs:

- **Server** — Port, HTTPS certificate, API key, rate limiting
- **Model** — Default model, offloading config, context compression defaults
- **Agent** — Iteration limits, auto-commit thresholds, plan approval requirements
- **Plugin** — Registry URL, update check interval, sandbox policy
- **Privacy** — Conversation encryption toggle, export format preferences

## Using the Inference Server

### Starting the Server

1. Go to the **Server** tab
2. Click **Start Server**
3. The server starts on port 8080 by default (configurable)
4. The status indicator shows **Running (Port 8080)**

### API Endpoints

The server exposes endpoints compatible with the OpenAI and Anthropic APIs:

```bash
# Chat completions (OpenAI-compatible)
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "default",
    "messages": [{"role": "user", "content": "Hello"}]
  }'

# Image generation
curl http://localhost:8080/v1/images/generations \
  -H "Content-Type: application/json" \
  -d '{
    "model": "stable-diffusion-xl",
    "prompt": "a sunset over mountains",
    "size": "1024x1024"
  }'
```

### Streaming Responses

Enable streaming with the `X-Stream: true` header for real-time token delivery via Server-Sent Events.

## Loading Models

### GGUF Models (Text Generation)

1. Navigate to **Models** → click **Add Model**
2. Select a `.gguf` file
3. The model is indexed and appears in the model list
4. Click **Load Model** to load it into memory

### Safetensor Models (Image Generation)

1. Navigate to **Image Gen** tab
2. Select a diffusion model from the dropdown
3. Models are automatically discovered from the models directory

### Model Storage

Models are stored in the platform-specific application data directory:
- **Windows**: `%APPDATA%\OpenLMStudio\models\`
- **macOS**: `~/Library/Application Support/OpenLMStudio/models/`
- **Linux**: `~/.config/OpenLMStudio/models/`

## Image Generation

### Basic Generation

1. Select a model (SD 1.5, SDXL, SD 3, or Flux)
2. Enter your prompt (e.g., "a sunset over mountains")
3. Optionally enter a negative prompt
4. Configure parameters:
   - **Resolution**: 512x512, 768x768, 1024x1024, 1280x720
   - **Steps**: Number of denoising steps (1–50)
   - **CFG Scale**: Classifier-free guidance scale (1.0–20.0)
   - **Seed**: Fixed seed for reproducibility (or use 🎲 for random)
5. Click **Generate Image**

### Advanced Features

- **Negative Prompt** — Describe what you don't want in the output
- **Batch Generation** — Generate multiple images at once
- **LoRA Adapters** — Apply style LoRAs for consistent aesthetics

## Context Management

### Understanding Context Budget

The context budget shows how much of your model's context window is used:

- 🟢 **Green** — Healthy, no compression needed
- 🟡 **Yellow** — Approaching limit, compression will begin
- 🔴 **Red** — Critical, lowest-relevance segments will be evicted

### Pin & Suppress Controls

Each message in the chat can be controlled individually:

- **📌 Pin** — Prevents the message from being compressed or evicted
- **👁️/🚫 Suppress/Reveal** — Hides a message from the AI's context

### Adding Custom Context

Add custom context that's always included:

1. Click **+ Add Custom Context** in the Context panel
2. Select the injection type (System Prompt, File Contents, or Raw Context)
3. Enter the content
4. Click **Inject Context**

## Agent Harness

### Executing Tasks

The agent harness allows autonomous task completion with plan/act cycles:

1. **Planning Phase** — The agent analyzes the task and proposes a plan
2. **Acting Phase** — The agent executes actions using available tools
3. **Reviewing Phase** — The agent verifies completion

### Available Tools

- **File Read/Write/Patch** — Safe file operations
- **Command Execute** — Sandboxed command execution
- **Search Files** — Regex search across project files
- **Git Operations** — Diff, blame, branch listing, history
- **Project Explorer** — Real-time file tree with changes
- **MCP Tools** — Tools from connected MCP servers

## Plugin Management

### Installing Plugins

1. Open the **Plugin** tab in settings
2. Configure the registry URL (optional)
3. Search for available plugins
4. Install plugins with one click
5. Enable/disable plugins as needed

### Security

Plugins run in a sandbox (cgroups v2 on Linux/macOS, Job Objects on Windows) to isolate them from the main application.

## Troubleshooting

### Server Won't Start

- Check that port 8080 is not in use
- Verify HTTPS certificate permissions
- Review the log output in the developer console

### Model Loading Fails

- Ensure the model file is not corrupted (verify SHA256 hash)
- Check available GPU VRAM (models larger than VRAM fall back to CPU)
- Verify the model format (GGUF for text, Safetensors for image)

### Images Not Generating

- Ensure an image generation model is loaded
- Check the Image Gen tab for error messages
- Verify the ONNX Runtime is properly installed

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Enter | Send message |
| Ctrl+New | New chat |
| Ctrl+S | Start/Stop server |

## Data Privacy

Conversations can be encrypted at rest using AES-256 encryption. Enable this in Settings → Privacy.

## Export & Import

Chats can be exported/imported in JSON format:
- **Export**: Save conversation history for archival
- **Import**: Restore from a previous export