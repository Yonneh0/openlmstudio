using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents progress information during a model download operation.
/// </summary>
public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    double Percentage,
    long SpeedBytesPerSecond,
    bool DiskSpaceWarning = false
);

/// <summary>
/// Event arguments for download progress updates.
/// </summary>
public class DownloadProgressEventArgs : EventArgs
{
    /// <summary>The number of bytes downloaded so far.</summary>
    public long BytesDownloaded { get; set; }

    /// <summary>Total number of bytes to download (0 if unknown).</summary>
    public long TotalBytes { get; set; }

    /// <summary>Progress percentage (0-100). 0 if total is unknown.</summary>
    public double Percentage { get; set; }

    /// <summary>Current download speed in bytes per second.</summary>
    public long SpeedBytesPerSecond { get; set; }

    /// <summary>Whether a disk space warning has been raised for this download.</summary>
    public bool DiskSpaceWarning { get; set; } = false;
}

/// <summary>
/// Event arguments for download completion.
/// </summary>
public class DownloadCompletedEventArgs : EventArgs
{
    /// <summary>The path of the downloaded file (or partial path on failure).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Whether the download completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the download failed, or null on success.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>SHA256 hash of the downloaded file (if computed during validation).</summary>
    public string? Sha256Hash { get; set; }

    /// <summary>MD5 hash of the downloaded file (if computed during validation).</summary>
    public string? Md5Hash { get; set; }

    /// <summary>Model type classification for multi-modal models. Null for GGUF text generation models.</summary>
    public Domain.Models.ModelType? ModelType { get; set; }

    /// <summary>Whether the file was validated via hash comparison against a known-good manifest.</summary>
    public bool HashVerified { get; set; } = false;
}

/// <summary>
/// Event arguments for disk space warnings during download.
/// </summary>
public class DiskSpaceWarningEventArgs : EventArgs
{
    /// <summary>Available disk space in bytes before this download starts.</summary>
    public long AvailableBeforeBytes { get; set; }

    /// <summary>Available disk space in bytes after this download completes (may be negative if insufficient space).</summary>
    public long AvailableAfterBytes { get; set; }

    /// <summary>Percentage of remaining capacity. 0 = full, 5 = critical (5% left).</summary>
    public double RemainingCapacityPercent { get; set; }
}

/// <summary>
/// Event arguments for LoRA adapter merge operation progress.
/// </summary>
public class LoraMergeEventArgs : EventArgs
{
    /// <summary>The current step number in the merge process.</summary>
    public int CurrentStep { get; set; }

    /// <summary>Total number of steps expected.</summary>
    public int TotalSteps { get; set; }

    /// <summary>Progress percentage (0-100).</summary>
    public double Percentage { get; set; }
}

/// <summary>
/// Interface for downloading model files from various sources.
/// </summary>
public interface IDownloadManager : IDisposable
{
    /// <summary>Raised when download progress updates (approximately every second).</summary>
    event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

    /// <summary>Raised when a download completes or fails.</summary>
    event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

    /// <summary>Raised when disk space warnings occur during download (at 80%, 90%, 95% thresholds).</summary>
    event EventHandler<DiskSpaceWarningEventArgs>? DiskSpaceWarning;

    /// <summary>Raised when a LoRA adapter merge operation is in progress.</summary>
    event EventHandler<LoraMergeEventArgs>? LoraMergeProgress;

    // ---- HuggingFace Authentication ----

    /// <summary>Whether the current download manager has a valid HuggingFace access token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Sets the HuggingFace access token manually. Overrides any cached token.</summary>
    void SetHuggingFaceToken(string token);

    // ---- Download Methods ----

    /// <summary>Downloads a model file from a HuggingFace repository URL.</summary>
    Task<string?> DownloadFromHuggingFaceAsync(string repoId, string filename, string downloadPath);

    /// <summary>Downloads a safetensors model file from HuggingFace with SHA256 hash verification (optional).</summary>
    Task<string?> DownloadSafetensorsModelAsync(string repoId, string filename, string downloadPath, string? expectedSha256 = null);

    /// <summary>Downloads a diffusion checkpoint (Stable Diffusion/Flux) — handles single-file and sharded formats.</summary>
    Task<string?> DownloadDiffusionCheckpointAsync(string repoId, string destinationPath);

    /// <summary>Downloads a LoRA adapter (LoRa/LoHa/LoKr format) with optional base model merge.</summary>
    Task<string?> DownloadLoraAdapterAsync(string repoId, string? baseModelPath, string destinationPath, bool mergeWithBase = false);

    /// <summary>Downloads a file from an arbitrary URL.</summary>
    Task<string?> DownloadFromUrlAsync(string url, string downloadPath);

    // ---- HuggingFace API Integration ----

    /// <summary>Lists all files in a HuggingFace repository (requires authentication for gated repos).</summary>
    Task<IReadOnlyList<HfRepoFileInfo>?> ListRepositoryFilesAsync(string repoId, string? revision = null);

    /// <summary>Gets the SHA256 hash of an LFS file from HuggingFace's ETag header.</summary>
    Task<string?> GetFileSha256HashAsync(string repoId, string filename);

    // ---- Verification Methods ----

    /// <summary>Validates that a downloaded GGUF file is readable and has valid headers.</summary>
    Task<bool> ValidateGgufFileAsync(string filePath);

    /// <summary>Validates that a downloaded safetensors file has valid header integrity.</summary>
    Task<bool> ValidateSafetensorsFileAsync(string filePath);

    /// <summary>Verifies SHA256 hash of a downloaded file against an expected value.</summary>
    Task<bool> VerifySha256HashAsync(string filePath, string? expectedSha256);

    /// <summary>Verifies MD5 hash of a downloaded file against an expected value (legacy).</summary>
    Task<bool> VerifyMd5HashAsync(string filePath, string? expectedMd5);

    // ---- Model Management Methods ----

    /// <summary>Gets the local path for a model by ID (searches common locations). Null if not found.</summary>
    Task<string?> GetLocalModelPathAsync(string modelId);

    /// <summary>Merges a LoRA adapter's weights into a base model. Returns true on success.</summary>
    Task<bool> MergeLoraAdapterAsync(string baseModelPath, string loraAdapterPath, string? outputPath, double scalingFactor = 1.0);

    /// <summary>Gets the estimated available disk space on the target drive (bytes). Null if unavailable.</summary>
    Task<long?> GetAvailableDiskSpaceAsync(string targetPath);

    // ---- Cancellation ----

    /// <summary>Cancels any active downloads.</summary>
    void CancelDownloads();
}

