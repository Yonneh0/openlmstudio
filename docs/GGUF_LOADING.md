# GGUF Loading Pipeline

## Overview

OpenLMStudio loads local LLM models in GGUF (GPT-Generated Unified Format) through a multi-layered pipeline:

1. **GgufParser** — Reads GGUF headers from `.gguf` files to extract metadata
2. **GgufModelDownloader** — Discovers local models and downloads from HuggingFace
3. **EngineBinaryDownloader** — Downloads llama-server binaries from GitHub releases
4. **MainAIManager** — Orchestrates engine binary download, model loading, and server start
5. **MainModelSelector** — UI component that handles user interaction

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
| Endianness | Little-endian | Little-endian |

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

## GgufModelDownloader

Manages model discovery and downloading:

```
GgufModelDownloader
├── DiscoverModelsAsync()    — Find all .gguf files in download directory
├── DownloadFromHuggingFaceAsync() — Download from HuggingFace Hub
├── GetRecommendation()      — Get recommended settings
└── UpdateLastUsedAsync()    — Update usage metadata
```

### Download Directory

Default: `%APPDATA%/OpenLMStudio/models`

### Filename Collision Prevention

When downloading from HuggingFace, the downloader prefixes filenames with the repo ID to prevent collisions:
- `model.gguf` from `org1` → `org1_model.gguf`
- `model.gguf` from `org2` → `org2_model.gguf`

## EngineBinaryDownloader

Downloads llama-server binaries from GitHub releases:

```
EngineBinaryDownloader
├── DownloadForBackendAsync()    — Download for specific backend (CPU, CUDA, Metal, Vulkan)
├── CheckForUpdateAsync()        — Check for new releases
├── FindExtractedBinary()        — Locate binary in extracted archive
└── ValidateBinaryLocally()      — Run --help to validate
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

### Port Allocation

Each loaded model gets a unique port from the range **4200–4400**:
- Ports are allocated sequentially
- Port reuse detection prevents conflicts
- Port wraps around when the range is exhausted

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

## MainModelSelector (UI)

The Avalonia UI control for model management:

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