using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Image preview service for live preview updates during generation.
/// </summary>
public class ImagePreviewService : IImagePreviewService
{
    private byte[]? _currentPreview;
    private readonly object _lock = new();

    public event Action<ImageGenerationProgress>? OnPreviewUpdated;

    public Task UpdatePreviewAsync(ImageGenerationProgress progress, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _currentPreview = progress.ImageBytes;
        }
        OnPreviewUpdated?.Invoke(progress);
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetCurrentPreviewAsync(CancellationToken ct = default)
    {
        lock (_lock)
            return Task.FromResult(_currentPreview);
    }

    public void ClearPreview()
    {
        lock (_lock)
            _currentPreview = null;
    }

    public void Dispose() { }
}