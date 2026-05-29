using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for live image preview updates during generation.
/// </summary>
public interface IImagePreviewService : IDisposable
{
    /// <summary>
    /// Event raised when a preview image is updated.
    /// </summary>
    event Action<ImageGenerationProgress>? OnPreviewUpdated;

    /// <summary>
    /// Updates the preview with a thumbnail or intermediate image.
    /// </summary>
    Task UpdatePreviewAsync(ImageGenerationProgress progress, CancellationToken ct = default);

    /// <summary>
    /// Gets the current preview image bytes (null if no preview available).
    /// </summary>
    Task<byte[]?> GetCurrentPreviewAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears the current preview.
    /// </summary>
    void ClearPreview();
}