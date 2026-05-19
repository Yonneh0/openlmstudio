# OpenLMStudio API Compatibility Matrix

## Overview
OpenLMStudio implements OpenAI-compatible and Anthropic-compatible API endpoints for local model inference. This document lists which endpoints support which model types.

## Chat Completions (`/v1/chat/completions`)
**Supported model types:** TextGeneration (GGUF only)
**Inference engine:** llama.cpp (`LlamaCppChatCompletionService`)
**Streaming:** Yes (SSE)
**Example:**
```json
POST /v1/chat/completions
{
  "model": "llama-3-8b-instruct.Q5_K_S.gguf",
  "messages": [{"role": "user", "content": "Hello"}],
  "stream": true
}
```

## Image Generation (`/v1/images/generations`)
**Supported model types:** ImageGeneration (Safetensors/Diffusion models)
**Inference engine:** ONNX Runtime (`DiffusionPipelineService`)
**Streaming:** Yes (SSE via `X-Stream` header)
**Example:**
```json
POST /v1/images/generations
{
  "model": "sdxl-base-1.0",
  "prompt": "a sunset over mountains",
  "width": 1024,
  "height": 1024,
  "steps": 30,
  "cfg_scale": 7.5
}
```

## Image Inpainting (`/v1/images/inpainting`)
**Supported model types:** ImageGeneration with mask support
**Example:**
```json
POST /v1/images/inpainting
{
  "model": "inpainting-model",
  "prompt": "replace sky with sunset",
  "init_image": "data:image/png;base64,...",
  "mask_image": "data:image/png;base64,..."
}
```

## Image Outpainting (`/v1/images/outpainting`)
**Supported model types:** ImageGeneration with outpainting mask
**Example:**
```json
POST /v1/images/outpainting
{
  "model": "sdxl-base-1.0",
  "prompt": "extend the scene",
  "init_image": "data:image/png;base64,...",
  "direction": "right"
}
```

## Embeddings (`/v1/embeddings`)
**Supported model types:** Embedding (Safetensors)
**Inference engine:** ONNX Runtime (`EmbeddingPipelineService`)
**Status:** Stub — generates random normalized vectors until safetensors integration complete

## Anthropic Messages (`/v1/messages`)
**Supported model types:** TextGeneration (GGUF only)
**Inference engine:** llama.cpp
**Response format:** Not yet fully compatible with Anthropic format

## Model Listing (`/v1/models`)
**Endpoint:** `GET /v1/models`
**Returns:** All models from `IModelRepository.SearchMultiModalModelsAsync()`
**Model types:** All (TextGeneration, ImageGeneration, Diffusion, VAE, LoRA, Embedding)

## Image Model Listing (`/v1/models/image/list`)
**Returns:** Models of type ImageGeneration/Diffusion only
**Endpoint:** `GET /v1/models/image/list`

---

## Unsupported Features

| Feature | Status |
|---------|--------|
| Audio transcription/translation | Not implemented |
| Multi-modal (vision) models | Not implemented |
| Fine-tuning endpoints | Not implemented |
| Batch image generation (non-streaming) | Implemented |
| ControlNet preprocessing | Stub (Canny/Depth/OpenPose) |
| IP-Adapter face conditioning | Stub |
| LoRA weight injection into ONNX sessions | Stub |

---

## Model Format Compatibility

| Format | Text Generation | Image Generation | Embedding | VAE | LoRA |
|--------|----------------|-----------------|-----------|-----|------|
| GGUF | ✅ | ❌ | ❌ | ❌ | ❌ |
| Safetensors | ❌ | ✅ | ✅ | ✅ | ✅ |
| ONNX | ❌ | ❌ | ❌ | ❌ | ❌ |