# Remaining Items for OpenLMStudio

This file captures the items from `DEVELOPMENT_PLAN.md` that still need attention, verified against the actual codebase as of 2026-05-22.

---

## 1. LoRA Runtime Tensor Injection (Phase 3.7) — Stubbed

**Status:** Weight extraction works, but `LoraAdapterManager.ApplyDeltasToSessionAsync` returns `true` without actually modifying ONNX Runtime tensors.

**Evidence:**
- `LoraWeightMerger` extracts delta tensors from safetensors and caches them
- `DiffusionInferenceEngine.ApplyLoraDeltas()` (line 195) **does** apply deltas during denoising — this is real, not stubbed
- `LoraAdapterManager.ApplyAdapterAsync` (called from `DiffusionPipelineService.GenerateImageAsync`) applies LoRA adapters via the engine's denoising loop

**What needs work:**
- The path where LoRA is applied via `DiffusionPipelineService` (not `LoraAdapterManager` directly) is functional
- The `LoraAdapterManager` → `ApplyDeltasToSessionAsync` path needs ONNX tensor manipulation API (not available in .NET bindings)
- Consider using ONNX Runtime's `InferenceSession.OnnxTensor` manipulation or switching to a LoRA-aware inference approach

**Effort:** Low

---

## 2. Streaming Image Generation (Phase 3.6) — Stubbed

**Status:** `DiffusionPipelineService.StreamProgressAsync` yields `ImageGenerationProgress` per step but doesn't emit real image bytes during denoising.

**Evidence:**
- `DiffusionPipelineService` (line 737): real `GenerateImageAsync` connected to full 3-stage pipeline
- `StreamProgressAsync` (line 737): yields progress per step with simulated `Task.Delay(100)` — not real SSE streaming of image bytes
- Server endpoint `/v1/images/generations` handles `X-Stream` header

**What needs work:**
- Emit partial image bytes per denoising step via SSE (convert intermediate latents to PNG at each step)
- Wire up server SSE to stream partial images instead of just progress events

**Effort:** Medium

---

## 3. ActiveProjectWatcher (Phase 7.5) — Stubbed

**Status:** Interface and implementation exist, but needs real filesystem watcher for large projects.

**Evidence:**
- `ActiveProjectWatcher.cs` exists
- `IActiveProjectWatcher` interface exists
- Plan notes: "needs real filesystem watcher for large projects"

**What needs work:**
- Implement real `FileSystemWatcher` with proper path filtering
- Handle file system events efficiently (batching, debouncing)
- Support large projects with many files (path filtering, ignore patterns)

**Effort:** Low-Medium

---

## 4. Performance Benchmarking (Phase 10.1) — Stubbed

**Status:** `ChatCompletionBenchmark` exists but not fully tested against real server.

**Evidence:**
- Plan notes: "model loading time, token generation throughput"
- Benchmark stub exists

**What needs work:**
- Implement `ChatCompletionBenchmark` against real server
- Implement model loading time benchmark
- Token generation throughput benchmark

**Effort:** Low

---

## 5. Load Testing (Phase 10.1) — Stubbed

**Status:** `ServerLoadTest` exists but not fully tested against real server.

**Evidence:**
- Plan notes: "load testing for server endpoints under concurrent request scenarios"
- Test stub exists

**What needs work:**
- Implement `ServerLoadTest` against real server
- Test concurrent requests to `/v1/chat/completions`, `/v1/images/generations`, `/v1/messages`

**Effort:** Low

---

## 6. ImagePostProcessingService.DecodeLatentsToPng (Phase 3.9) — Stubbed

**Status:** Returns placeholder PNG via nearest-neighbor interpolation, not real VAE decode.

**Evidence:**
- `ImagePostProcessingService` exists
- `DecodeLatentsToPng` returns nearest-neighbor interpolation result
- `DiffusionInferenceEngine.DecodeLatents` (line 235) has real PNG output via SkiaSharp

**What needs work:**
- Wire up `DiffusionInferenceEngine.DecodeLatents` to `ImagePostProcessingService.DecodeLatentsToPng`
- Real VAE decode via ONNX Runtime tensor manipulation

**Effort:** Low

---

## 7. Agent System Prompt Generator (Phase 7.7) — Partial

**Status:** `AgentSystemPromptGenerator` (181 lines) has all 3 methods implemented.

**Evidence:**
- `GeneratePlanningPrompt` — fully implemented
- `GenerateActingPrompt` — fully implemented
- `GenerateNextActionSuggestions` — fully implemented with context-aware logic

**What needs work:**
- Dynamic assembly of system prompt based on current task context (currently generates per-call)
- Context-aware suggestions for next action (partially done — suggestions are heuristic-based)
- Consider merging with `PinguPromptGenerator` for unified prompt generation

**Effort:** Low

---

## Summary

| # | Item | Phase | Effort | Status |
|---|------|-------|--------|--------|
| 1 | LoRA runtime tensor injection | 3.7 | Low | Partial |
| 2 | Streaming image generation | 3.6 | Medium | Stubbed |
| 3 | ActiveProjectWatcher real implementation | 7.5 | Low-Medium | Stubbed |
| 4 | Performance benchmarking | 10.1 | Low | Stubbed |
| 5 | Load testing | 10.1 | Low | Stubbed |
| 6 | ImagePostProcessingService.DecodeLatentsToPng | 3.9 | Low | Stubbed |
| 7 | AgentSystemPromptGenerator dynamic assembly | 7.7 | Low | Partial |

**Total: 7 items remaining**

The plan is **~85% complete** with only low-effort items remaining.