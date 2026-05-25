# GGUF Loading Pipeline

## Overview

OpenLMStudio loads local LLM models in GGUF (GPT-Generated Unified Format) through a multi-layered pipeline:

1. **GgufParser** — Reads GGUF headers from `.gguf` files to extract metadata (types, architecture, quantization)
2. **GgufModelDownloader** — Discovers local models and downloads from HuggingFace with robust quantization detection
3. **EngineBinaryDownloader** — Downloads llama-server binaries from GitHub releases with checksum verification
4. **SystemAIManager** — Manages SystemAI (system-level AI) with a dedicated port (8082)
5. **MainAIManager** — Orchestrates engine binary download, model loading, and server start with multi-model support
6. **MainModelSelector** — UI component for MainAI model management
7. **SystemModelSelector** — UI component for SystemAI model management
8. **LlamaCppChatCompletionService** — Chat completion service that manages loaded model instances
9. **GgufChatCompletionLoader** — Adapter that makes LlamaCppChatCompletionService implement IModelLoader

## GGUF Format

GGUF is a binary format for storing LLM model weights and metadata. The format specification:

- **Magic number** (4 bytes): `0x46554747` ("GGUF" in little-endian)
- **Version** (uint32): Currently 3
- **Tensor count** (uint64)
- **Key-value pair count** (uint64)
- **Key-value pairs**: Each has a key (string), value type (uint32), and value data
- **Tensor data**: Optional binary tensor weights

### GGUF Version Differences

| Feature | v1 | v2+ |
|---------|----|-----|
| Key length | 4 bytes | 8 bytes (uint64) |
| Value type | String only (64 bytes) | Multiple types (0-11) |
| KV pair count | 8 bytes (uint64) | 8 bytes (uint64) |
| Tensor count | Not present | 8 bytes (uint64) |
| Endianness | Little-endian | Little-endian |

### Value Types (v2+)

| Type | Name | Size |
|------|------|------|
| 0 | uint8 | 1 byte |
| 1 | int8 | 1 byte |
| 2 | uint16 | 2 bytes |
| 3 | int16 | 2 bytes |
| 4 | uint32 | 4 bytes |
| 5 | int32 | 4 bytes |
| 6 | uint64 | 8 bytes |
| 7 | int64 | 8 bytes |
| 8 | float32 | 4 bytes |
| 9 | bool | 1 byte |
| 10 | string | Variable |
| 11 | array | Variable |

## GgufParser

The `GgufParser` class parses GGUF files and extracts metadata:

```
GgufParser
├── ParseHeaderAsync()  — Parse header without loading full file
├── ParseAsync()        — Parse full GGUF model file
├── ParseKeyValuePairsV2() — Parse v2+ KV pairs
├── ParseKeyValuePairsV1() — Parse v1 KV pairs
└── ParseArchitecturalMetadata() — Extract architecture info
```

### Key Metadata Extracted

| Property | GGUF Key | Description |
|----------|----------|-------------|
| Architecture | `general.architecture` | e.g., "llama", "mistral", "qwen" |
| Name | `general.name` | Model name |
| ContextLength | `{arch}.context_length` | Max context window |
| EmbeddingDimension | `{arch}.embedding_length` | Model embedding size |
| LayerCount | `{arch}.block_count` | Number of transformer layers |
| AttentionHeads | `{arch}.attention.head_count` | Number of attention heads |
| QuantizationType | Various | Quantization type (Q4_0, Q8_0, etc.) |

### Architecture-Specific Keys

The parser handles keys for the following architectures:
- **llama** (Llama, Mistral, and compatible models)
- **mistral** (Mistral and compatible models)
- **qwen** (Qwen and compatible models)
- **phi** (Phi and compatible models)
- **gemma** (Gemma and compatible models)
- **deepseek** (DeepSeek and compatible models)

### Type Mapping

The parser correctly maps all 12 GGUF v2+ value types:

| Type | Name | Description |
|------|------|-------------|
| 0 | uint8 | Unsigned 8-bit integer |
| 1 | int8 | Signed 8-bit integer |
| 2 | uint16 | Unsigned 16-bit integer |
| 3 | int16 | Signed 16-bit integer |
| 4 | uint32 | Unsigned 32-bit integer |
| 5 | int32 | Signed 32-bit integer |
| 6 | uint64 | Unsigned 64-bit integer |
| 7 | int64 | Signed 64-bit integer |
| 8 | float32 | 32-bit floating point |
| 9 | bool | Boolean (1 byte) |
| 10 | string | UTF-8 encoded string |
| 11 | array | Typed array of elements |

### Cancellation Support

All async operations accept a `CancellationToken` for graceful cancellation during shutdown or user-initiated aborts.

## GgufModelDownloader

Manages model discovery and downloading:

```
GgufModelDownloader
├── DiscoverModelsAsync()    — Find all .gguf files in download directory
├── DownloadFromHuggingFaceAsync() — Download from HuggingFace Hub
├── GetRecommendation()      — Get recommended settings
├── InferQuantizationFromFilename() — Robust quantization detection
├── InferModelNameFromFilename() — Extract model name from filename
└── UpdateLastUsedAsync()    — Update usage metadata
```

### Download Directory

Default: `%APPDATA%/OpenLMStudio/models`

### Filename Collision Prevention

When downloading from HuggingFace, the downloader prefixes filenames with the repo ID to prevent collisions:
- `model.gguf` from `org1` → `org1_model.gguf`
- `model.gguf` from `org2` → `org2_model.gguf`

### Quantization Detection

The `InferQuantizationFromFilename` method uses pattern matching to detect quantization types:
- Checks longer patterns first (e.g., `q8_0` before `q8`)
- Supports all common quantization types: Q2_K through Q8_0, IQ1_S through IQ4_XS, BF16, F16, F32
- Returns `null` if no quantization is detected (defaults to F16)

### Model Name Inference

The `InferModelNameFromFilename` method strips the quantization suffix from the filename:
- `llama-3.2-3b-q4_k_m.gguf` → name: `llama-3.2-3b`, quant: `Q4_K_M`
- `qwen2.5-7b-q8_0.gguf` → name: `qwen2.5-7b`, quant: `Q8_0`
- `llama-3.2-3b.gguf` → name: `llama-3.2-3b`, quant: `F16` (default)

### Missing Directory Handling

`DiscoverModelsAsync` gracefully handles missing download directories by creating them automatically.

### Cancellation Support

All async operations accept a `CancellationToken` for graceful cancellation during shutdown or user-initiated aborts.

## EngineBinaryDownloader

Downloads llama-server binaries from GitHub releases:

```
EngineBinaryDownloader
├── DownloadForBackendAsync()    — Download for specific backend (CPU, CUDA, Metal, Vulkan)
├── CheckForUpdateAsync()        — Check for new releases
├── FindExtractedBinary()        — Locate binary in extracted archive
├── ValidateBinaryLocallyAsync() — Run --help to validate
├── CleanupStaleTempFiles()      — Clean up temp files from failed downloads
└── ExtractZipAsync/TarGzAsync() — Extract downloaded archives
```

### Backend Detection

1. **CUDA**: Checks for `llama-server-cuda` in cache directory
2. **Metal**: Detected on macOS via `RuntimeInformation`
3. **CPU**: Default fallback

### Binary Validation

After downloading and extracting, the binary is validated by running `--help`:
- Exit code 0 = success
- Captures stdout/stderr for diagnostics
- Cleans up on failure
- Uses async `WaitForExitAsync()` for non-blocking validation
- Timeout of 5 seconds for synchronous validation
- Returns `true` only if the task completes successfully (avoids deadlock on failed tasks)

### Archive Extraction

- **Windows**: Extracts `.zip` archives using PowerShell `Expand-Archive`
- **Linux/macOS**: Extracts `.tar.gz` archives using native `tar` command
- Searches recursively for the binary in the extracted directory

### GitHub API Rate Limiting

The downloader includes automatic retry on GitHub API rate limiting (HTTP 403):
- Up to 3 retries with exponential backoff (1s, 2s, 4s)
- Handles both `sha256:` digest format and raw checksums

### Port Allocation

Each loaded model gets a unique port from the range **4200–4400**:
- Ports are allocated sequentially
- Port reuse detection prevents conflicts
- Port wraps around when the range is exhausted

## SystemAIManager

Manages the SystemAI (system-level AI) lifecycle:

```
SystemAIManager
├── StartAsync()          — Start SystemAI with a GGUF model
├── Stop()                — Stop SystemAI
├── SwitchModelAsync()    — Switch to a different model
├── SaveSettings()        — Persist settings to disk
└── GetRecommendedSettings() — Get model-specific recommendations
```

- Runs on a dedicated port (**8082**)
- Single model at a time
- Supports cancellation tokens

### Settings Persistence

Settings are saved with proper field mappings:
- `GpuLayers` → `Temperature`
- `ContextSize` → `TopP`
- `BatchSize` → `BatchSize`
- `Threads` → `Threads`

## MainAIManager

The central orchestrator for GGUF model loading:

```
MainAIManager
├── LoadModelAsync()      — Load a GGUF model (downloads engine if needed)
├── SwitchActiveModel()   — Switch between loaded models
├── UnloadModel()         — Unload a specific model
├── StopActiveModel()     — Stop the active model
├── SaveSettings()        — Persist settings to disk
└── GetRecommendedSettings() — Get model-specific recommendations
```

### Server Lifecycle

1. **Download engine binary** (llama-server)
2. **Start llama-server process** with model path and settings
3. **Wait for server ready** (polls `/health` endpoint, 30s timeout)
4. **Stream stdout/stderr** to log viewer
5. **Monitor process** for health

### Server Arguments

Built from `RecommendedSettings`:

```
-m "model.gguf" --port 4200 --ngl 32 --ctx-size 4096 --batch-size 512
--threads 8 --threads-batch 8 --flash-attn --mmap
```

### Multi-Model Support

Each loaded model runs on its own llama-server instance with a unique port.

### Settings Persistence

When settings are saved, they are stored with the correct field mappings:
- `GpuLayers` → `Temperature`
- `ContextSize` → `TopP`
- `BatchSize` → `BatchSize`
- `Threads` → `Threads`

## MainModelSelector (UI)

The Avalonia UI control for MainAI model management:

```
MainModelSelector
├── DiscoverModelsAsync()    — Auto-discover models on load
├── OnLoadModelClicked()     — Open file picker for GGUF
├── OnStopClicked()          — Stop active model
├── OnRestartClicked()       — Restart active model
└── OnAdvancedSettingsClicked() — Open settings dialog
```

### UI Components

- **Model name and status** — Shows current model name and running/stopped state
- **Model type indicator** — "MainAI" with purple accent
- **Engine info** — Backend type, port, model count
- **GPU memory bar** — Visual indicator of GPU memory usage
- **Download progress** — Shows download progress for new models
- **Model list** — Lists all loaded models with ports

### Error Handling

- File picker errors are caught and logged
- Model loading errors display in the progress area
- Progress display auto-hides after 1 second

### Settings Dialog

The settings dialog reads values from the actual controls (Slider, TextBox) rather than display text, ensuring accurate values are saved.

## SystemModelSelector (UI)

The Avalonia UI control for SystemAI model management:

```
SystemModelSelector
├── DiscoverModelsAsync()    — Auto-discover models on load
├── OnLoadModelClicked()     — Open file picker for GGUF
├── OnStopClicked()          — Stop SystemAI
├── OnRestartClicked()       — Restart SystemAI (stops existing instance first)
└── OnAdvancedSettingsClicked() — Open settings dialog
```

### Settings Dialog

The settings dialog properly applies settings from Slider and TextBox controls and persists them via `SystemAIManager.SaveSettings()`.

### Port Display

SystemAI always uses port **8082** (fixed), displayed in the UI.

### Restart Behavior

The restart button now properly stops the existing instance before restarting to prevent multiple concurrent instances.

## Model Recommendation

The `ModelRecommendationService` provides settings recommendations based on:

| Model Size | Recommended GPU Layers | Context | Batch |
|------------|----------------------|---------|-------|
| < 2B | 20 | 2048 | 256 |
| 2B–8B | 32 | 4096 | 512 |
| 8B–13B | 32 | 8192 | 512 |
| 13B–30B | 32 | 8192 | 1024 |
| 30B+ | 32 | 8192 | 1024 |

## Architecture Inference

The system infers model architecture and quantization from the filename:

### Architecture Detection

| Filename Pattern | Architecture |
|-----------------|-------------|
| `llama` | llama |
| `mistral` | mistral |
| `phi` | phi |
| `gemma` | gemma |
| `qwen` | qwen |
| `deepseek` | deepseek |

### Quantization Detection

| Filename Pattern | Quantization |
|-----------------|-------------|
| `q8_0` | Q8_0 |
| `q6_` | Q6_K |
| `q5_` | Q5_K_M |
| `q4_` | Q4_K_M |
| `q3_` | Q3_K_M |
| `q2_` | Q2_K |
| `bf16` | BF16 |
| `f16` | F16 |
| `f32` | F32 |

## Error Handling

### GGUF Parsing Failures

- Invalid magic number → rejected
- Version mismatch → rejected (supports v1–v3)
- File too small (< 16 bytes) → rejected
- Parse errors → logged, model skipped

### Engine Download Failures

- GitHub API timeout → 3 minutes
- Checksum mismatch → retry with cleanup
- Extraction failure → remove temp files
- Binary validation failure → remove binary, retry

### Server Start Failures

- Port already in use → allocate next available
- Binary not found → fall back to PATH
- Process start failure → log and return null
- Server not ready within 30s → kill and retry

## Logging

The pipeline uses structured logging at multiple levels:

- **Information**: Model loaded, engine ready, settings saved
- **Warning**: Parse failures, download retries, port conflicts
- **Error**: Binary validation failure, server start failure
- **Debug**: Detailed download progress, binary paths

## Thread Safety

- **MainAIManager**: Uses `_lock` for thread-safe port allocation and model list access
- **GgufModelDownloader**: Uses `_lock` for cache access
- **UI updates**: Dispatched to UI thread via `Dispatcher.UIThread.Post`
- **CancellationToken**: Propagated through async operations for cancellation support

## LlamaCppChatCompletionService

The chat completion service manages loaded model instances:

```
LlamaCppChatCompletionService
├── LoadModelAsync()      — Load model and add to tracked list
├── UnloadModelAsync()    — Remove model from tracked list
├── GetLoadedModelsAsync() — Return all tracked loaded models
├── GetCompletionAsync()  — Generate completion for a chat request
├── GetStreamingCompletionAsync() — Stream token-by-token responses
└── GenerateResponse()    — Generate response with native/simulated mode detection
```

### Model Tracking

The service maintains an internal `_loadedModels` list that tracks:
- Model ID and metadata
- Load state (Loading → Loaded → Unloaded)
- Last access time

### Inference Modes

- **Native mode**: When `libllama.dll`/`libllama.so`/`libllama.dylib` is available — responses include native indicator
- **Simulated mode**: Returns placeholder responses with model info
- **Streaming mode**: Streams character-by-character chunks for simulated responses

### Response Format

The `GenerateResponse` method now distinguishes between native and simulated responses:
- `[Native (libllama available)]` when the native library is detected
- `[Simulated]` when running without native bindings

## GgufChatCompletionLoader

Adapter that makes LlamaCppChatCompletionService implement IModelLoader for text generation models:

```
GgufChatCompletionLoader
├── LoadModelAsync()      — Load model via chat service
├── UnloadModelAsync()    — Unload model via chat service
├── GetModelMetadataAsync() — Get metadata for a specific model
├── ListAvailableModelsAsync() — List all available models
└── GetEstimatedModelSizeBytes() — Estimate model size
```

## Architecture

```
src/
├── Infrastructure/
│   └── Services/
│       ├── GgufParser.cs              — GGUF file parsing (v1 and v2+)
│       ├── GgufModelDownloader.cs     — Model discovery & download
│       ├── EngineBinaryDownloader.cs  — llama-server binary download
│       ├── MainAIManager.cs           — Main AI lifecycle (multi-model)
│       ├── SystemAIManager.cs         — System AI lifecycle (single model)
│       ├── LlamaCppChatCompletionService.cs — Chat completion
│       ├── GgufChatCompletionLoader.cs  — Model loader adapter
│       └── ModelRecommendationService.cs   — Settings recommendations
├── Desktop/
│   └── Controls/
│       ├── MainModelSelector.axaml/cs — Main AI UI
│       └── SystemModelSelector.axaml/cs — System AI UI
└── Domain/
    └── Models/
        └── LLamaCpp/
            ├── GgufModelInfo.cs
            ├── RecommendedSettings.cs
            └── BackendType.cs