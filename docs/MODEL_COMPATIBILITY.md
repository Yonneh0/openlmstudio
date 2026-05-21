# Model Compatibility Guide

## Overview

OpenLMStudio supports multiple model formats for different AI tasks. This guide lists known-compatible models and any known issues.

## Text Generation (GGUF)

### Recommended Models

| Model | GGUF Format | Notes |
|-------|-------------|-------|
| Llama 3.1 8B | Q5_K_S | Balanced quality/speed |
| Llama 3.1 70B | Q4_K_M | Requires ~40GB VRAM |
| Mistral 7B v3 | Q5_K_S | Good for general tasks |
| Mixtral 8x7B | Q4_K_M | Mixture of experts |
| Phi-3 Mini | Q5_K_S | Small, efficient |
| Gemma 2 9B | Q5_K_S | Google's model |
| Qwen 2.5 7B | Q5_K_S | Strong coding ability |

### GGUF Quantization Guide

| Format | VRAM (8B model) | Quality | Use Case |
|--------|-----------------|---------|----------|
| Q4_0 | ~4.5 GB | Good | Fast inference on low-VRAM |
| Q4_1 | ~5 GB | Better | Small VRAM, better quality |
| Q5_0 | ~5.5 GB | Very good | Balanced |
| Q5_1 | ~6 GB | Excellent | Recommended for most use cases |
| Q8_0 | ~8 GB | Best | Maximum quality |

## Image Generation (Safetensors)

### Supported Model Families

| Model Family | Formats | Typical Size | Notes |
|--------------|---------|--------------|-------|
| Stable Diffusion 1.5 | Safetensors | ~2 GB | Fast, good for low-end hardware |
| SDXL 1.0 | Safetensors | ~6.7 GB | High quality, requires ~8GB VRAM |
| SD 3.5 Medium | Safetensors | ~5.6 GB | Latest SD variant |
| Flux.1 Dev | Safetensors | ~23 GB | Highest quality, requires significant VRAM |
| Flux.1 Fast | Safetensors | ~23 GB | Faster inference, fewer steps needed |

### Recommended Settings

| Model | Resolution | Steps | CFG | Sampler |
|-------|------------|-------|-----|---------|
| SD 1.5 | 512x512 | 20-30 | 7.0 | Euler a |
| SDXL | 1024x1024 | 25-35 | 7.5 | DPM++ 2M |
| SD 3.5 | 1024x1024 | 20-30 | 4.5 | DPM++ 2M |
| Flux Dev | 1024x1024 | 25-50 | 3.5 | Euler |
| Flux Fast | 1024x1024 | 4-8 | 3.5 | Euler |

### LoRA Adapters

| LoRA Type | Compatible Models | Format |
|-----------|-------------------|--------|
| Standard LoRA | SD 1.5, SDXL, Flux | Safetensors |
| LoHa | SD 1.5, SDXL | Safetensors |
| LoKr | SD 1.5, SDXL | Safetensors |

## Embedding Models (Safetensors)

| Model | Dimensions | Use Case |
|-------|------------|----------|
| sentence-transformers/all-MiniLM-L6-v2 | 384 | General-purpose embeddings |
| BAAI/bge-small-en-v1.5 | 384 | Semantic search |
| BAAI/bge-large-en-v1.5 | 1024 | Higher-quality embeddings |

## Known Issues

### Flux.1 Models
- Require significant VRAM (>20 GB recommended)
- May fail on systems with less than 16 GB VRAM
- CFG scale should be set to 3.5 (not the default 7.5)

### SDXL Models
- Inpainting models require a specific checkpoint (e.g., `sdxl-inpainting`)
- Outpainting works best with SDXL-specific models

### Embedding Models
- Currently generate random normalized vectors — real inference pending safetensors integration

## Model Download

Models can be downloaded from HuggingFace:

```csharp
// Example using the DownloadManager
await downloadManager.DownloadModelAsync(
    modelId: "stabilityai/sdxl-turbo",
    modelType: ModelType.ImageGeneration,
    cancellationToken: ct);
```

Or manually:
1. Download the `.gguf` or `.safetensors` file
2. Place it in the models directory
3. The model will be auto-discovered and indexed

## Model Storage Location

- **Windows**: `%APPDATA%\OpenLMStudio\models\`
- **macOS**: `~/Library/Application Support/OpenLMStudio/models/`
- **Linux**: `~/.config/OpenLMStudio/models/`

## Verifying Model Integrity

Models downloaded via OpenLMStudio are automatically verified with SHA256. To verify manually:

```bash
# Windows
CertUtil -hashfile model.gguf SHA256

# macOS
shasum -a 256 model.gguf

# Linux
sha256sum model.gguf