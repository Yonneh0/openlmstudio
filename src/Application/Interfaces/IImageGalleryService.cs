using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for the image gallery service backed by SQLite.
/// </summary>
public interface IImageGalleryService : IDisposable
{
    /// <summary>
    /// Gets recent images from the gallery.
    /// </summary>
    Task<IReadOnlyList<ImageGalleryEntry>> GetRecentImagesAsync(int count = 50, CancellationToken ct = default);

    /// <summary>
    /// Searches the gallery by prompt text.
    /// </summary>
    Task<IReadOnlyList<ImageGalleryEntry>> SearchImagesAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Gets a single image entry by ID.
    /// </summary>
    Task<ImageGalleryEntry?> GetImageAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Deletes an image from the gallery.
    /// </summary>
    Task DeleteImageAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Exports the gallery as JSON for backup.
    /// </summary>
    Task<string> ExportGalleryAsJsonAsync(CancellationToken ct = default);

    /// <summary>
    /// Imports a gallery from JSON.
    /// </summary>
    Task ImportGalleryFromJsonAsync(string json, CancellationToken ct = default);
}