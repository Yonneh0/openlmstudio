namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents the current update status.
/// </summary>
public enum UpdateStatus
{
    Current,
    UpdateAvailable,
    Downloading,
    DownloadComplete,
    Applying,
    DownloadFailed
}

/// <summary>
/// Information about an available update.
/// </summary>
public record UpdateInfo(
    string Version,
    string ReleaseNotes,
    long DownloadSizeBytes,
    DateTime PublishedDate);

/// <summary>
/// Interface for application and plugin auto-update management.
/// </summary>
public interface IUpdateManager : IDisposable
{
    /// <summary>
    /// Checks for a new application version.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the update (if available).
    /// </summary>
    Task<bool> DownloadUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies the downloaded update (typically requires restart).
    /// </summary>
    Task<bool> ApplyUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current update status.
    /// </summary>
    UpdateStatus CurrentStatus { get; }

    /// <summary>
    /// Progress of the current update download (0-100).
    /// </summary>
    double DownloadProgress { get; }

    /// <summary>
    /// Checks for plugin updates.
    /// </summary>
    Task<IReadOnlyList<UpdateInfo>> CheckPluginUpdatesAsync(CancellationToken ct = default);
}