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
**Pipeline:** CLIP text encoding → UNet denoising with CFG → VAE decoding
**Samplers:** Euler, Euler a, DPM++, LMS, Heun
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
**Pipeline:** CLIP text encoding → UNet denoising with mask blending → VAE decoding
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
**Pipeline:** Canvas expansion → outpainting mask blending → UNet denoising → VAE decoding
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
**Status:** Real ONNX Runtime inference with proper tokenization, attention mask, and mean-pooling
**Example:**
```json
POST /v1/embeddings
{
  "model": "embedding-model",
  "input": "Hello world"
}
```

## Anthropic Messages (`/v1/messages`)
**Supported model types:** TextGeneration (GGUF only)
**Inference engine:** llama.cpp
**Response format:** Enhanced — supports `cache_control` (ephemeral), `stop_sequence`, `thinking` blocks, and cache token tracking

## Model Listing (`/v1/models/list`)
**Endpoint:** `GET /v1/models/list`
**Returns:** All models from `IModelRepository.SearchMultiModalModelsAsync()`
**Model types:** All (TextGeneration, ImageGeneration, Diffusion, VAE, LoRA, Embedding)

## Image Model Listing (`/v1/models/image/list`)
**Returns:** Models of type ImageGeneration/Diffusion only
**Endpoint:** `GET /v1/models/image/list`

## LoRA Adapters
**Supported:** Dynamic LoRA stacking with configurable scaling factors
**Format:** Safetensors (LoRA/LoHa/LoKr)
**Integration:** `LoraWeightMerger` + `LoraAdapterManager` — runtime adapter application
**Note:** Weight injection into ONNX Runtime sessions is stubbed pending tensor manipulation

## Unsupported Features

| Feature | Status |
|---------|--------|
| Audio transcription/translation | Not implemented |
| Multi-modal (vision) models | Not implemented |
| Fine-tuning endpoints | Not implemented |
| ControlNet Depth/OpenPose | Stub (Canny working) |
| IP-Adapter face conditioning | Stub |
| LoRA weight injection into ONNX sessions | Stub (infrastructure in place) |

## Model Format Compatibility

| Format | Text Generation | Image Generation | Embedding | VAE | LoRA |
|--------|----------------|-----------------|-----------|-----|------|
| GGUF | ✅ | ❌ | ❌ | ❌ | ❌ |
| Safetensors | ❌ | ✅ | ✅ | ✅ | ✅ |
| ONNX | ❌ | ❌ | ❌ | ❌ | ❌ |