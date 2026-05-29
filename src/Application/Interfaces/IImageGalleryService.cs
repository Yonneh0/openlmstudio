using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for the image gallery service with SQLite-backed storage.
/// </summary>
public interface IImageGalleryService : IDisposable
{
    Task<ImageGalleryEntry> AddImageAsync(ImageGalleryEntry entry, CancellationToken ct = default);
    Task<ImageGalleryEntry?> GetImageAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<ImageGalleryEntry>> GetRecentImagesAsync(int count = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ImageGalleryEntry>> SearchImagesAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<ImageGalleryEntry>> GetAllImagesAsync(CancellationToken ct = default);
    Task DeleteImageAsync(string id, CancellationToken ct = default);
    Task ExportGalleryAsJsonAsync(string filePath, CancellationToken ct = default);
    Task ImportGalleryFromJsonAsync(string filePath, CancellationToken ct = default);
}