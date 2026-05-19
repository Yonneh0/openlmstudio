# OpenLMStudio Model Compatibility Guide

## Supported Model Types

### GGUF Models (Text Generation)
- **Engine:** llama.cpp via `LlamaCppChatCompletionService`
- **Supported variants:** GGUF, GGML
- **Quantization support:** Q4_0, Q4_1, Q5_0, Q5_1, Q6_K, Q8_0, IQ2_XXS, IQ2_XS, Q2_K, Q3_K_S, Q3_K_M, Q3_K_L, Q4_K_S, Q4_K_M, Q5_K_S, Q5_K_M, Q6_K, Q8_0
- **Recommended models:**
  - Llama 3.x Instruct (8B, 70B)
  - Mistral 7B Instruct
  - Mixtral 8x7B Instruct
  - Phi-3 Mini/Medium
  - Gemma 2B/9B Instruct

### Safetensors Models (Image Generation, Diffusion, VAE, LoRA, Embedding)
- **Engine:** ONNX Runtime via `DiffusionPipelineService`, `VAEPipelineService`, `EmbeddingPipelineService`
- **Supported families:**
  - Stable Diffusion 1.4, 1.5, 2.1
  - SDXL 1.0
  - SD 3.x (partial)
  - Flux (Dev / Fast variants — architecture ready, not tested with specific models)
- **Recommended models:**
  - `stabilityai/stable-diffusion-xl-base-1.0`
  - `stabilityai/stable-diffusion-2-1`
  - `runwayml/stable-diffusion-v1-5`

### LoRA Adapters (Safetensors)
- **Merge types:** LoRA, LoHa, LoKr
- **Application:** Runtime stacking with configurable scaling via `ILoraAdapterManager`
- **Supported base models:** Any Safetensors-based image generation model
- **Recommended adapters:**
  - CivitAI LoRA adapters (safetensors format)
  - HuggingFace LoRA adapters

## Model Loading Notes

### GPU Memory Requirements
| Model | Minimum VRAM (FP16) | Minimum VRAM (Q4) |
|-------|---------------------|--------------------|
| Llama 3.1 8B | 8 GB | 5 GB |
| Llama 3.1 70B | 80 GB | 35 GB |
| SDXL 1.0 | 6 GB | N/A (Safetensors) |
| SD 1.5 | 4 GB | N/A (Safetensors) |

### Context Length
- Maximum context window: 32,768 tokens (configurable per model via GGUF `context_length` metadata)
- Recommended context: 4,096–8,192 tokens for most models

### Performance Expectations
| Model | Approximate Output (tokens/sec) |
|-------|---------------------------------|
| Llama 3.1 8B (RTX 4090) | ~60-80 |
| Llama 3.1 8B (RTX 3060) | ~30-40 |
| Llama 3.1 70B (A100 80GB) | ~20-30 |
| SDXL 1.0 (RTX 4090) | ~3-5s per image (30 steps) |

## Known Issues Per Model Variant

### Llama 3.1 70B GGUF
- **Issue:** May OOM on consumer GPUs with 24GB VRAM
- **Workaround:** Use Q4_K_M or lower precision mode; enable CPU fallback
- **Fix:** `ModelLoadingFallbackService` auto-retries with CPU

### SDXL Inpainting
- **Issue:** Mask blending artifacts at edges
- **Workaround:** Increase mask padding by 2-4 pixels
- **Status:** Being addressed in upcoming patch

### Flux Dev
- **Issue:** Requires very high VRAM (24GB+) for stable results
- **Workaround:** Use Flux Fast variant (fewer steps)
- **Status:** Pipeline architecture ready, specific model not tested

### Embedding Models
- **Issue:** Current implementation generates random vectors (stub)
- **Status:** Real ONNX Runtime inference implemented, awaiting safetensors model integration

---

## Quick Troubleshooting

### "Model failed to load"
1. Check GPU VRAM — use `nvidia-smi` or `vulkaninfo`
2. Try lower quantization (Q4_K_M → Q3_K_M)
3. Enable CPU fallback in settings

### "Out of memory"
1. Reduce context length in model settings
2. Close other GPU-consuming applications
3. Enable model offloading (CPU/GPU split)

### Streaming disconnects
1. Check SSE buffer size settings
2. Verify server is running with HTTPS enabled
3. Restart server — stale SSE connections may need cleanup

### "Hash verification failed"
1. Delete corrupted model file
2. Re-download — `DownloadManager` verifies SHA256/MD5 on completion