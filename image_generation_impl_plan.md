



# Image Generation System — Complete Implementation Plan

## Part 1: Missing/Incomplete Features

### A. T5 Text Encoder for Flux (Critical)

**Problem:** Flux.2 uses T5-XL + CLIP, not CLIP alone. Current `DiffusionInferenceEngine` only loads CLIP.

**Implementation:**
1. Add `LoadT5Encoder()` method to `DiffusionInferenceEngine` — loads T5-XL ONNX model as a separate session
2. Add `RunT5Encoder()` method — processes text through T5 to produce [1, seq_len, 4096] embeddings
3. Modify `GenerateImageAsync()` to detect Flux models and use T5 + CLIP instead of CLIP alone
4. Add `BlendT5AndClipEmbeddings()` for the CFG blending of T5 embeddings (Flux uses T5 for unconditional + conditional)
5. Add T5-specific time step schedules (Flux uses different noise schedules)

**Files:** `DiffusionInferenceEngine.cs`, `DiffusionPipelineService.cs`

### B. Flux DiT (Diffusion Transformer) Architecture

**Problem:** Flux uses DiT (transformer blocks), not traditional UNet convolutions.

**Implementation:**
1. Add `RunDiTDenoise()` method that handles DiT-style attention-based denoising
2. Update `GetUnetTensorNames()` to detect DiT tensor naming conventions (`attn.*`, `mlp.*`, `norm.*`)
3. Add `ApplyAttentionMask()` for Flux's cross-attention pattern
4. Update `RunUnetDenoise()` to dispatch to DiT path for Flux models
5. Add DiT-specific parameters: `num_attention_heads`, `attention_head_dim`, `num_layers`

**Files:** `DiffusionInferenceEngine.cs`, `DiffusionModelFamilyService.cs`

### C. Flux.2 Specific Model Config

**Current:** Generic "Flux" family (16 channels, CFG 1-3.5, 20-35 steps)

**Implementation:**
1. Add `Flux.2` family config:
   - Pipeline type: `flux2`
   - Latent channels: 16
   - Default width/height: 1024
   - Recommended steps: 25-50 (Flux.2 works well with fewer steps at high quality)
   - CFG: 1.0-3.5 (Flux.2 uses low CFG)
   - Supported samplers: Euler, Euler A, Heun, DPM++ 2M, DPM++ SDE
   - File pattern: `flux2*`, `flux.2*`
2. Add `Flux.1-dev` config for older Flux variants
3. Add `Flux.1-schnell` config (quantized, faster)

**Files:** `DiffusionModelFamilyService.cs`

### D. Image Save to Disk

**Current:** Images returned as base64 data URI only.

**Implementation:**
1. Add `SaveImageAsync()` to `IDiffusionPipelineService` — saves PNG to user-selected directory
2. Add `SaveImageToDirectory()` with configurable path (default: `~/Pictures/OpenLMStudio/`)
3. Add `SaveImageWithMetadata()` — saves alongside a JSON sidecar with generation params (prompt, seed, model, CFG, steps, sampler)
4. Add `GenerateTimestampedFilename()` — `IMG_20260528_143022.png`
5. Add `SaveImageAsBase64()` — returns base64 string for API responses

**Files:** `DiffusionPipelineService.cs`, `IDiffusionPipelineService.cs`

### E. Model Discovery Enhancement

**Current:** Pattern-based discovery in `JsonModelRepository`.

**Implementation:**
1. Add `DiscoverFluxModelsAsync()` — scans for `*.safetensors` files with T5 encoder files
2. Add `DetectModelType()` — auto-detects SD1.5/SDXL/Flux based on tensor shapes in safetensors
3. Add `InferLatentChannelsFromModel()` — reads first safetensors file to determine latent channels
4. Add `ScanForLoRAAdapters()` — recursive scan of `~/.openlmstudio/models/` for LoRA files
5. Add `ScanForVAEModels()` — discovers standalone VAE files

**Files:** `JsonModelRepository.cs`, `DiffusionModelFamilyService.cs`

### F. Image Gallery/History

**Current:** No persistent image gallery.

**Implementation:**
1. Add `IImageGalleryService` interface
2. Add `ImageGalleryEntry` record: Id, Prompt, ModelId, Width, Height, Seed, CfgScale, Steps, Sampler, FilePath, ThumbnailPath, Timestamp
3. Implement `ImageGalleryService` with SQLite-backed storage
4. Add `GetRecentImages()`, `SearchImages()`, `DeleteImage()`, `GetImageAsync()`
5. Auto-generate thumbnails (128x128) when saving
6. Add `ExportGalleryAsJson()` for backup

**Files:** `IImageGalleryService.cs`, `ImageGalleryService.cs`

### G. Batch Generation

**Current:** Single image generation only.

**Implementation:**
1. Add `GenerateBatchAsync()` to `IDiffusionPipelineService`
2. Support multiple seeds, multiple prompts, grid layout output
3. Add `ImageBatchResult` with all generated images + combined grid image
4. Add `CreateGridImage()` — stitches multiple images into a grid (2x2, 3x3, etc.)

**Files:** `DiffusionPipelineService.cs`, `IDiffusionPipelineService.cs`

### H. Progress Preview Streaming

**Current:** `StreamProgressAsync()` yields per-step progress with intermediate PNGs.

**Implementation:**
1. Add `IImagePreviewService` for live preview updates
2. Add `OnImagePreviewUpdated` event to `IDiffusionPipelineService`
3. Update `StreamProgressAsync()` to emit preview thumbnails at every N steps
4. Add `UpdatePreviewAsync()` for UI to receive streaming previews

**Files:** `DiffusionPipelineService.cs`, `IImagePreviewService.cs`

---

## Part 2: UI Tab Redesign — Image Generation Tab

### Design Philosophy
- **Fluid control** — everything accessible in one tab, no deep nesting
- **Real-time feedback** — see progress as it generates
- **Model-aware** — auto-detect model capabilities and present relevant controls
- **Gallery-integrated** — generated images flow into gallery

### Tab Layout (Avalonia 12)

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Image Generation                                          [📷 Gallery]  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─ Model Selection ─────────────────────────────────────────────────┐  │
│  │  [🔍 Search...]  [Flux.2 ▼]  [Load Model] [Unload]              │  │
│  │  Status: ● Loaded | ○ Loading | ○ Unloaded                       │  │
│  │  VRAM: █████████░░ 8.2/16 GB | Model: flux2-dev.safetensors     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─ Prompt ──────────────────────────────────────────────────────────┐  │
│  │  [A beautiful sunset over mountains, golden hour, cinematic light] │  │
│  │  ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ──  │  │
│  │  Negative: [avoid blur, low quality, deformed]                     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─ Parameters ──────────────────────────────────────────────────────┐  │
│  │  Width:  [1024]  Height: [1024]  Steps: [30]  CFG: [3.5]         │  │
│  │  Sampler: [Euler ▼]  Seed: [42]  Images: [1]                     │  │
│  │  [Presets: 512² 768² 1024² 1024² 1344²]                          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─ LoRA Adapters ───────────────────────────────────────────────────┐  │
│  │  [📎 Add LoRA]  [🗑 Clear All]                                     │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │  🎨 anime_style.safetensors  Weight: 0.8  [━ ━ ━ ━ ━] [✕] │  │  │
│  │  │  🎨 detailed_face.safetensors  Weight: 0.5  [━ ━ ━ ━] [✕]  │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─ Generate ────────────────────────────────────────────────────────┐  │
│  │  [▶ Generate] [⏸ Pause] [⏹ Stop]  |  Progress: ███████░░ 70%     │  │
│  │  [💾 Save to Disk] [📋 Copy to Clipboard]                        │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─ Preview ─────────────────────────────────────────────────────────┐  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │                                                              │  │  │
│  │  │              [Image Preview Area]                            │  │  │
│  │  │              (Updates in real-time during generation)        │  │  │
│  │  │                                                              │  │  │
│  │  └──────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌─ Recent Images ───────────────────────────────────────────────────┐  │
│  │  [🖼] [🖼] [🖼] [🖼] [🖼] [🖼] [🖼] [🖼] [🖼] [🖼]  [View All →]   │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Key UI Components (Avalonia 12)

#### 1. ModelSelectorControl
- **Searchable dropdown** with model thumbnails
- **Load/Unload buttons** with loading spinner animation
- **VRAM indicator** with color-coded bar (green → yellow → red)
- **Model info tooltip** (type, latent channels, recommended settings)

#### 2. ParameterSliderGroup
- **Width/Height** with linked/unlinked toggle (chain icon)
- **Steps slider** (1-100) with step preview
- **CFG slider** (0.1-20) with model-specific range
- **Sampler selector** with descriptions on hover
- **Seed** with randomize button (🎲)
- **Resolution presets** row: `512²` `768²` `1024²` `1344²`

#### 3. LoRAAdapterCard
- **Weight slider** (0-2) with visual feedback
- **Remove button** (✕)
- **Auto-apply indicator** (checkmark when applied)
- **Compact mode** (slider only, no name)

#### 4. ImagePreviewArea
- **Real-time updates** during generation (streaming)
- **Zoom controls** (100%, fit, 200%)
- **Pan/scroll** for high-res images
- **Before/after** comparison for inpainting
- **Image info overlay** (hover to see params)

#### 5. RecentImagesGallery
- **Horizontal scrollable** row of thumbnails
- **Click to reload** params from saved image
- **Right-click menu**: Open, Save As, Delete, Copy
- **Empty state** with "View Full Gallery" button

### Avalonia 12 Features to Use

1. **Animation System** — `Animation` for smooth transitions on model load, parameter changes
2. **DataGrid/TreeDataGrid** — for gallery view
3. **ColorPicker** — for custom background colors
4. **MarkdownContentControl** — for model descriptions
5. **PopupControl** — for tooltips and popovers
6. **VisualLayerRenderer** — for smooth image rendering
7. **Fluent Theme** — native Fluent Design styling
8. **Xaml Compilation** — for performance
9. **ReactiveUI Bindings** — for reactive UI updates
10. **Styles in App.axaml** — centralized theming

### Implementation Order

1. **Core Services** (Day 1-2): T5 encoder, DiT, Flux config, image save, gallery
2. **UI Components** (Day 3-4): ModelSelector, ParameterGroup, LoRACard, PreviewArea
3. **Main Tab** (Day 5): Assemble all components into ImageGenerationTab
4. **Integration** (Day 6): Wire up all events, streaming, real-time updates
5. **Polish** (Day 7): Animations, tooltips, keyboard shortcuts, edge cases

---

## Part 3: File Changes Summary

| File | Change |
|------|--------|
| `DiffusionInferenceEngine.cs` | +T5 encoder, +DiT support, +T5/CLIP blending |
| `DiffusionPipelineService.cs` | +SaveImage, +BatchGenerate, +GridImage |
| `IDiffusionPipelineService.cs` | +SaveImageAsync, +GenerateBatchAsync, +IImagePreviewService |
| `DiffusionModelFamilyService.cs` | +Flux.2, +Flux.1-dev, +Flux.1-schnell configs |
| `VaEPipelineService.cs` | Minor: better tensor name detection |
| `LoraAdapterManager.cs` | +Recursive LoRA scan |
| `ImageGalleryService.cs` | **NEW** — SQLite gallery with thumbnails |
| `IImageGalleryService.cs` | **NEW** — gallery interface |
| `JsonModelRepository.cs` | +Auto-detect model type |
| `MainWindow.ImageGeneration.cs` | **REWRITE** — new tab implementation |
| `ImageGenerationTab.axaml` | **NEW** — polished XAML layout |
| `ImageGenerationTab.axaml.cs` | **NEW** — code-behind |
| `ModelSelectorControl.axaml` | **NEW** — model selector component |
| `ParameterSliderGroup.axaml` | **NEW** — parameter controls |
| `LoRAAdapterCard.axaml` | **NEW** — LoRA card component |
| `ImagePreviewArea.axaml` | **NEW** — preview component |
| `RecentImagesGallery.axaml` | **NEW** — gallery row |
| `App.axaml` | +Styles for new components |
| `MainWindow.axaml` | Updated Image tab reference |

---

## Part 4: Key Technical Decisions

1. **T5 vs CLIP detection**: Check safetensors weight keys — if `text_model.*` exists with shape > 2048, it's T5. Otherwise CLIP.
2. **DiT dispatch**: Check `num_hidden_layers` in model config — if > 0 and attention heads present, use DiT path.
3. **Flux CFG**: Flux uses `guidance` as a scalar (1-3.5), not traditional CFG. Handle both modes.
4. **Image save**: Use `SaveFileDialog` from Avalonia 12 with PNG format filter.
5. **Gallery**: SQLite with `PRAGMA journal_mode=WAL` for concurrent reads during generation.
6. **Streaming**: Use `IAsyncEnumerable<ImageGenerationProgress>` with `OnNext` pattern for UI updates.

---




Here's the clean plan — what actually exists and what needs to be built:

# Image Generation System — Clean Implementation Plan

## What Actually Exists

**Core (in e:/Projects/AI/OpenLMStudio):**
- `DiffusionInferenceEngine.cs` — CLIP text encoder + UNet denoising + VAE decode (549 lines)
- `DiffusionPipelineService.cs` — main orchestrator with inpainting/outpainting/streaming (1378 lines)
- `VaEPipelineService.cs` — VAE encode/decode (434 lines)
- `LoraAdapterManager.cs` — LoRA loading and application (297 lines)
- `ImagePostProcessingService.cs` — upscaling, ControlNet, HiRes.fix (733 lines)
- `DiffusionModelFamilyService.cs` — SD1.5/SDXL/SD3/Flux configs
- `ImageTypes.cs` — OpenAI-compatible image types
- `IDiffusionPipelineService.cs` — interfaces
- `MainWindow.ImageGeneration.cs` — image generation in MainWindow (280 lines)
- `MainWindow.axaml` — main shell

## What Needs to Be Built

### 1. Image-to-Image Pipeline
- Encode input image → latent space → add noise → denoise → decode
- Denoise strength slider (0-1) controls transformation amount
- Intuitive UI: drag-and-drop input, preview, generate

### 2. Image Format Converters
- PNG (lossless, transparency)
- JPEG (10-100 quality)
- WebP (modern, best compression)
- ICO (multiple sizes, Windows icon)
- BMP (uncompressed)
- GIF (animated, 256 colors)

### 3. Robust Save Options
- Save to disk with format selection
- Save with metadata sidecar (JSON with prompt, seed, model, CFG, steps)
- Save to gallery with thumbnails
- Save as multiple formats at once

### 4. SystemAI Control Interfaces
- `ImageGenerationCommand` record — all parameters SystemAI needs
- `ImageToImageCommand` — input image + denoise strength
- `InpaintCommand` — mask + prompt
- SystemAI issues commands → service executes → returns result

### 5. Polished UI Tab (Avalonia 12)
- Model selector with load/unload
- Prompt + negative prompt
- Image-to-image input with denoise slider
- Parameters: width, height, steps, CFG, seed, sampler
- LoRA adapters with weight sliders
- Output format selection
- Live preview during generation
- Recent images gallery row

## Implementation Order

1. **Interfaces** — IImageToImageService, IImageFormatConverter, IImageSaver
2. **Image-to-Image Service** — encode → noise → denoise → decode
3. **Format Converters** — PNG/JPEG/WebP/ICO/BMP/GIF
4. **Image Saver** — save to disk with metadata
5. **SystemAI Commands** — ImageGenerationCommand record
6. **Update DiffusionPipelineService** — wire up new features
7. **UI Components** — ImageGenerationTab.axaml
8. **Build verification** — dotnet build, INDEX.md updates
