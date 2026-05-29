
# Complete Image Generation System — Final Implementation Plan

## Current State Assessment

You've already created 7 new service files that were added externally:
- `T5TextEncoder.cs` — T5 XL text encoder for Flux
- `FluxDitEngine.cs` — Diffusion Transformer engine
- `Flux2ModelConfig.cs` — Flux.2 specific configuration
- `ImageSaveService.cs` — Image saving service
- `ImageBatchGenerationService.cs` — Batch generation
- `ImageModelDiscoveryService.cs` — Model discovery
- `INDEX.md` — Updated with new files

## Updated Plan — Phase by Phase

### Phase 1: Core Image-to-Image Pipeline (Foundation)

**New Types:**
- `ImageToImageRequest` — input image + prompt + denoise strength
- `ImageToImageResponse` — output image with metadata
- `ControlNetInput` — ControlNet preprocessed control image
- `InpaintRequest` — image + mask + prompt
- `OutpaintRequest` — image + direction + expansion size

**New Interfaces:**
```csharp
public interface IImageToImageService
{
    Task<ImageToImageResponse> EncodeAndDenoiseAsync(ImageToImageRequest request, CancellationToken ct);
    Task<ImageToImageResponse> ImageVariationAsync(ImageVariationRequest request, CancellationToken ct);
}

public interface IImageFormatConverter
{
    Task<byte[]> ToPngAsync(byte[] imageBytes);
    Task<byte[]> ToJpegAsync(byte[] imageBytes, int quality);
    Task<byte[]> ToWebPAsync(byte[] imageBytes, int quality);
    Task<byte[]> ToIcoAsync(byte[] imageBytes, int[] sizes);
    Task<byte[]> ToBmpAsync(byte[] imageBytes);
    Task<byte[]> ToGifAsync(byte[] frames, int delayMs);
}

public interface IImageSaver
{
    Task<string> SaveToDiskAsync(byte[] imageBytes, string filename, ImageFormat format);
    Task<string> SaveWithMetadataAsync(byte[] imageBytes, ImageMetadata metadata, ImageFormat format);
    Task SaveToGalleryAsync(ImageGalleryEntry entry);
}
```

**Key Implementation:**
1. `ImageToImageService` — encodes input image via VAE, applies noise schedule, runs denoising loop
2. `ImageFormatConverter` — SkiaSharp-based converters for PNG/JPEG/WebP/ICO/BMP/GIF
3. `ImageSaver` — uses `SaveWithMetadataAsync` with JSON sidecar containing generation params

### Phase 2: SystemAI Control Interfaces

**Since SystemAI controls image generation, interfaces must be declarative:**
```csharp
public record ImageGenerationCommand(
    string PipelineType,           // "sd15" | "sdxl" | "flux2"
    string Prompt,
    string? NegativePrompt,
    int Width,
    int Height,
    int Steps,
    double CfgScale,
    int Seed,
    string SamplerType,
    IReadOnlyList<LoraAdapterCommand>? LoRAAdapters,
    ImageToImageCommand? ImageToImage,
    ControlNetCommand? ControlNet,
    InpaintCommand? Inpaint,
    string OutputFormat,           // "png" | "jpeg" | "webp" | "ico"
    string? OutputPath);

public record LoraAdapterCommand(string ModelId, double Weight);
public record ImageToImageCommand(string InputImage, double DenoiseStrength);
public record ControlNetCommand(string ControlType, string ControlImage);
public record InpaintCommand(string MaskImage, string Prompt);
```

**SystemAI can issue commands like:**
```csharp
// Generate SDXL image
var command = new ImageGenerationCommand(
    PipelineType: "sdxl",
    Prompt: "A beautiful sunset",
    Width: 1024, Height: 1024,
    Steps: 30, CfgScale: 7.5,
    SamplerType: "dpm++",
    OutputFormat: "png",
    OutputPath: "C:\\Users\\Pictures\\output.png");

// Image-to-image with denoise
var i2iCommand = new ImageGenerationCommand(
    PipelineType: "sd15",
    Prompt: "A beautiful sunset",
    Width: 512, Height: 512,
    ImageToImage: new ImageToImageCommand(inputImageBytes, 0.75));

// Inpainting
var inpaintCommand = new ImageGenerationCommand(
    PipelineType: "sd15",
    Prompt: "A beautiful sunset",
    Inpaint: new InpaintCommand(maskBytes, "A beautiful sunset"));
```

### Phase 3: Image Format Converters

**SkiaSharp-based converters:**

| Format | Quality | Notes |
|--------|---------|-------|
| PNG | Lossless (100) | Default, supports transparency |
| JPEG | 10-100 | No transparency, smaller file size |
| WebP | 10-100 | Modern format, best compression |
| ICO | Multiple sizes | Windows icon, supports transparency |
| BMP | Lossless | Uncompressed, large files |
| GIF | Lossy | Animated support, 256 colors max |

**Implementation:**
```csharp
public class ImageFormatConverter : IImageFormatConverter
{
    public async Task<byte[]> ToPngAsync(byte[] imageBytes)
        => SKImage.FromEncodedData(imageBytes)
            .Encode(SKEncodedImageFormat.Png, 100)
            .ToArray();

    public async Task<byte[]> ToJpegAsync(byte[] imageBytes, int quality)
        => SKImage.FromEncodedData(imageBytes)
            .Encode(SKEncodedImageFormat.Jpeg, quality)
            .ToArray();

    public async Task<byte[]> ToIcoAsync(byte[] imageBytes, int[] sizes = null)
    {
        sizes ??= new[] { 16, 32, 48, 64, 128, 256 };
        var frames = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var scaled = SKBitmap.Decode(new MemoryStream(imageBytes))
                .Resize(new SKSizeI(size, size), SKSamplingOptions.Cubic);
            using var img = SKImage.FromBitmap(scaled);
            frames.Add(img.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        }
        return EncodeIco(frames);
    }
}
```

### Phase 4: Image-to-Image Pipeline

**Steps:**
1. Encode input image → latent space (via VAE)
2. Add noise based on denoise strength
3. Run denoising loop with prompt
4. Decode latents → image

**UI Controls for Image-to-Image:**
- **Input image** — drag-and-drop or browse
- **Denoise strength** — slider (0-1): 0 = keep original, 1 = full generation
- **Seed** — random or fixed
- **Steps** — how many denoising iterations
- **CFG scale** — guidance strength
- **Prompt** — text prompt
- **Negative prompt** — what to avoid

### Phase 5: UI Components (Avalonia 12)

**ImageGenerationTab.axaml** — Main tab layout:
```
┌─────────────────────────────────────────────────────────────────────┐
│  Image Generation                                        [Gallery]   │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─ Pipeline ───────────────────────────────────────────────────┐   │
│  │  [📷 Image→Image] [🎨 Generate] [🖼 Inpaint] [🔁 Variation]   │   │
│  │  Pipeline: [Flux.2 ▼]  Model: ● loaded  VRAM: 8.2/16 GB     │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ Prompt ─────────────────────────────────────────────────────┐   │
│  │  [A beautiful sunset over mountains, golden hour...]          │   │
│  │  ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ── ──    │   │
│  │  Negative: [avoid blur, low quality]                          │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ Image Input (for Image→Image) ──────────────────────────────┐   │
│  │  ┌──────────────────────────────────────────────────────────┐  │   │
│  │  │  [Input Image Preview]                                   │  │   │
│  │  └──────────────────────────────────────────────────────────┘  │   │
│  │  Denoise: [━ ━ ━ ━ ━ ━ ━ ━ ━ ━] 0.75                        │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ Parameters ─────────────────────────────────────────────────┐   │
│  │  Width: [1024]  Height: [1024]  Steps: [30]  CFG: [3.5]     │   │
│  │  Sampler: [Euler ▼]  Seed: [42]  Images: [1]                │   │
│  │  [Presets: 512² 768² 1024² 1344²]                           │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ LoRA Adapters ──────────────────────────────────────────────┐   │
│  │  [📎 Add LoRA]  [🗑 Clear All]                                │   │
│  │  ┌────────────────────────────────────────────────────────┐    │   │
│  │  │  🎨 anime_style  Weight: 0.8  [━ ━ ━ ━ ━] [✕]        │    │   │
│  │  └────────────────────────────────────────────────────────┘    │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ Output ─────────────────────────────────────────────────────┐   │
│  │  Format: [PNG ▼]  Path: [C:\Users\Pictures\...] [Browse]    │   │
│  │  [▶ Generate] [⏸ Pause] [⏹ Stop]  |  70% ███████░░         │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ Preview ────────────────────────────────────────────────────┐   │
│  │  ┌──────────────────────────────────────────────────────────┐  │   │
│  │  │  [Live Image Preview]                                    │  │   │
│  │  └──────────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌─ Recent ─────────────────────────────────────────────────────┐   │
│  │  [🖼] [🖼] [🖼] [🖼] [🖼] [🖼] [🖼] [🖼]  [View All →]        │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### Phase 6: SystemAI Integration

**SystemAI receives the ImageGenerationCommand and can control:**
- Model selection (pipeline type)
- Parameters (width, height, steps, CFG, seed)
- Input images (for image-to-image, inpainting)
- LoRA adapters
- Output format and path

**Example SystemAI workflow:**
```
1. SystemAI decides to generate an image
2. Issues: ImageGenerationCommand(
     PipelineType: "flux2",
     Prompt: "A beautiful sunset",
     Width: 1024, Height: 1024,
     Steps: 30, CfgScale: 3.5,
     SamplerType: "euler",
     LoRAAdapters: [new("anime_style", 0.8)],
     OutputFormat: "png",
     OutputPath: "C:\\Users\\Pictures\\sunset.png")
3. System calls DiffusionPipelineService.GenerateImageAsync(command)
4. Image is generated and saved
5. SystemAI receives ImageGenerationResult with path
```

## File Changes Summary

| File | Action | Notes |
|------|--------|-------|
| `IImageToImageService.cs` | **NEW** | Image-to-image interface |
| `ImageToImageService.cs` | **NEW** | Image-to-image implementation |
| `IImageFormatConverter.cs` | **NEW** | Format converter interface |
| `ImageFormatConverter.cs` | **NEW** | PNG/JPEG/WebP/ICO/BMP/GIF |
| `IImageSaver.cs` | **NEW** | Image saver interface |
| `ImageSaver.cs` | **NEW** | Save to disk with metadata |
| `ImageGenerationCommand.cs` | **NEW** | SystemAI command record |
| `ImageToImageRequest.cs` | **NEW** | I2I request type |
| `DiffusionPipelineService.cs` | **UPDATE** | Add I2I, save, format support |
| `IDiffusionPipelineService.cs` | **UPDATE** | Add new methods |
| `T5TextEncoder.cs` | **VERIFY** | Already created |
| `FluxDitEngine.cs` | **VERIFY** | Already created |
| `Flux2ModelConfig.cs` | **VERIFY** | Already created |
| `ImageSaveService.cs` | **VERIFY** | Already created |
| `ImageBatchGenerationService.cs` | **VERIFY** | Already created |
| `ImageModelDiscoveryService.cs` | **VERIFY** | Already created |
| `ImageGenerationTab.axaml` | **NEW** | Polished UI |
| `ImageGenerationTab.axaml.cs` | **NEW** | Tab code-behind |
| `MainWindow.axaml` | **UPDATE** | Add Image tab |
| `MainWindow.axaml.cs` | **UPDATE** | Tab wiring |
| `INDEX.md` | **UPDATE** | Document new files |

## Build Verification

Each phase will be followed by `dotnet build` to verify no errors or warnings.

## Ready to Proceed

Please **toggle to Act mode** when you're ready to begin implementation.