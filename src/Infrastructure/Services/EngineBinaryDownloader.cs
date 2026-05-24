using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.LLamaCpp;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Configuration for engine binary downloads.
/// </summary>
public record EngineDownloadConfig(
    string Version,
    string? ChecksumOverride = null,
    bool AutoUpdate = true);

/// <summary>
/// Information about an engine binary release from GitHub.
/// </summary>
public record EngineBinaryInfo(
    string Version,
    string DownloadUrl,
    string? Checksum,
    string Architecture);

/// <summary>
/// Downloads and caches llama.cpp engine binaries from GitHub releases.
/// Supports version pinning, checksum verification, and binary validation.
/// 
/// Downloads from the ggml-org/llama.cpp repository and handles the
/// archive-based release format (.zip for Windows, .tar.gz for Linux/macOS).
/// </summary>
public class EngineBinaryDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EngineBinaryDownloader> _logger;
    private readonly string _cacheDirectory;
    private readonly object _lock = new();
    private bool _disposed;

    public static readonly string DefaultVersion = "main";
    public static readonly string GitHubRepo = "ggml-org/llama.cpp";
    public static readonly TimeSpan ReleaseApiTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Creates a new EngineBinaryDownloader.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cacheDirectory">Optional custom cache directory. Defaults to %APPDATA%/OpenLMStudio/engines.</param>
    public EngineBinaryDownloader(ILogger<EngineBinaryDownloader>? logger = null, string? cacheDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines");
        _httpClient = new HttpClient { Timeout = DownloadTimeout };
    }

    /// <summary>
    /// Downloads the engine binary for the specified backend and returns the local path.
    /// Verifies SHA256 checksum if available in the release.
    /// Extracts the archive if necessary (Windows: .zip, Linux/macOS: .tar.gz).
    /// Validates the binary by running --help.
    /// </summary>
    /// <param name="backend">The backend type to download (CPU, CUDA, Metal, Vulkan).</param>
    /// <param name="config">Optional download configuration. Defaults to latest version.</param>
    /// <returns>The local path to the extracted binary.</returns>
    public async Task<string> DownloadForBackendAsync(BackendType backend, EngineDownloadConfig? config = null)
    {
        config ??= new EngineDownloadConfig(DefaultVersion);
        var version = config.Version;
        var platform = GetPlatform();
        var arch = GetArchitecture();

        var cachePath = Path.Combine(_cacheDirectory, backend.ToString().ToLower(), GetBinaryName(backend));

        // Check cache first — also validate the binary
        lock (_lock)
        {
            if (File.Exists(cachePath) && ValidateBinaryLocally(cachePath, backend))
            {
                _logger.LogDebug("Found valid cached binary for {Backend}: {Path}", backend, cachePath);
                return cachePath;
            }
        }

        // Clean up stale temp files from previous failed downloads
        CleanupStaleTempFiles();

        // Fetch the release from GitHub
        var releaseInfo = await GetReleaseAsync(version).ConfigureAwait(false);
        if (releaseInfo == null)
            throw new InvalidOperationException("Could not fetch release from GitHub");

        // Find the matching asset
        var asset = FindMatchingAsset(releaseInfo.Assets, backend, platform, arch);
        if (asset == null)
            throw new InvalidOperationException($"No matching binary found for {backend} on {platform}-{arch}");

        _logger.LogInformation("Downloading {Asset} for {Backend} from {Url}", asset.Name, backend, asset.BrowserDownloadUrl);

        // Download the archive
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var archivePath = cachePath + ".archive";
        var tempPath = cachePath + ".tmp";

        try
        {
            // Download the archive
            using var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var archiveStream = File.Create(archivePath);
            await stream.CopyToAsync(archiveStream).ConfigureAwait(false);

            // Verify checksum if available
            if (!string.IsNullOrEmpty(asset.Checksum))
            {
                var actualChecksum = await ComputeSha256Async(archivePath).ConfigureAwait(false);
                if (!string.Equals(actualChecksum, asset.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    CleanupTempFiles(_logger, archivePath, tempPath);
                    throw new InvalidOperationException(
                        $"Checksum verification failed for {backend} binary. Expected {asset.Checksum}, got {actualChecksum}");
                }
                _logger.LogDebug("Checksum verified for {Backend}", backend);
            }
            else if (!string.IsNullOrEmpty(config.ChecksumOverride))
            {
                var actualChecksum = await ComputeSha256Async(archivePath).ConfigureAwait(false);
                if (!string.Equals(actualChecksum, config.ChecksumOverride, StringComparison.OrdinalIgnoreCase))
                {
                    CleanupTempFiles(_logger, archivePath, tempPath);
                    throw new InvalidOperationException(
                        $"Checksum verification failed for {backend} binary. Expected {config.ChecksumOverride}, got {actualChecksum}");
                }
            }

            // Extract the archive to the final location
            var archiveType = GetArchiveType(asset.Name);
            if (archiveType == ArchiveType.Zip)
            {
                await ExtractZipAsync(archivePath, Path.GetDirectoryName(cachePath)!, tempPath, backend).ConfigureAwait(false);
            }
            else if (archiveType == ArchiveType.TarGz)
            {
                await ExtractTarGzAsync(archivePath, Path.GetDirectoryName(cachePath)!, tempPath, backend).ConfigureAwait(false);
            }
            else
            {
                // Unknown archive type — copy the archive directly and try to use it
                File.Copy(archivePath, tempPath, overwrite: true);
            }

            File.Move(tempPath, cachePath, overwrite: true);
            File.Delete(archivePath);

            // Set executable permissions on Unix systems
            if (platform is "linux" or "macos")
            {
                SetExecutablePermission(cachePath);
            }

            // Validate the binary
            if (!ValidateBinaryLocally(cachePath, backend))
            {
                File.Delete(cachePath);
                throw new InvalidOperationException($"Binary validation failed for {backend} at {cachePath}");
            }

            _logger.LogInformation("Downloaded and validated engine binary for {Backend} to {Path}", backend, cachePath);
            return cachePath;
        }
        catch
        {
            CleanupTempFiles(_logger, archivePath, tempPath);
            throw;
        }
    }

    /// <summary>
    /// Checks for new engine releases and returns version info if an update is available.
    /// </summary>
    /// <param name="backend">The backend type to check.</param>
    /// <returns>Engine binary info if an update is available, null otherwise.</returns>
    public async Task<EngineBinaryInfo?> CheckForUpdateAsync(BackendType backend)
    {
        var releaseInfo = await GetReleaseAsync(DefaultVersion).ConfigureAwait(false);
        if (releaseInfo == null)
            return null;

        var platform = GetPlatform();
        var arch = GetArchitecture();

        var asset = FindMatchingAsset(releaseInfo.Assets, backend, platform, arch);
        if (asset == null)
            return null;

        return new EngineBinaryInfo(
            Version: releaseInfo.TagName,
            DownloadUrl: asset.BrowserDownloadUrl,
            Checksum: asset.Checksum,
            Architecture: $"{platform}-{arch}");
    }

    /// <summary>
    /// Cleans up stale temp files from previous failed downloads.
    /// </summary>
    public void CleanupStaleTempFiles()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    return;

                foreach (var backendDir in Directory.GetDirectories(_cacheDirectory))
                {
                    foreach (var tempFile in Directory.GetFiles(backendDir, "*.tmp"))
                    {
                        try
                        {
                            File.Delete(tempFile);
                            _logger.LogDebug("Cleaned up stale temp file: {File}", tempFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to clean up stale temp file: {File}", tempFile);
                        }
                    }

                    foreach (var archiveFile in Directory.GetFiles(backendDir, "*.archive"))
                    {
                        try
                        {
                            File.Delete(archiveFile);
                            _logger.LogDebug("Cleaned up stale archive file: {File}", archiveFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to clean up stale archive file: {File}", archiveFile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up stale temp files");
            }
        }
    }

    /// <summary>
    /// Fetches the latest release from GitHub.
    /// </summary>
    private async Task<GithubRelease?> GetReleaseAsync(string version)
    {
        try
        {
            var url = string.IsNullOrEmpty(version) || version == DefaultVersion
                ? $"https://api.github.com/repos/{GitHubRepo}/releases/latest"
                : $"https://api.github.com/repos/{GitHubRepo}/releases/tags/{version}";

            _logger.LogDebug("Fetching release from {Url}", url);
            using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var assets = root.GetProperty("assets").EnumerateArray()
                .Select(a => new GithubAsset(
                    a.GetProperty("name").GetString() ?? "",
                    a.GetProperty("browser_download_url").GetString() ?? "",
                    a.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
                        ? ExtractChecksumFromDigest(digest.GetString() ?? "")
                        : null))
                .ToList();

            return new GithubRelease(
                root.GetProperty("tag_name").GetString() ?? "",
                assets);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch release from GitHub");
            return null;
        }
    }

    /// <summary>
    /// Extracts the SHA256 checksum from a digest string like "sha256:abc123...".
    /// </summary>
    private static string ExtractChecksumFromDigest(string digest)
    {
        if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            return digest.Substring("sha256:".Length);
        return digest;
    }

    /// <summary>
    /// Finds the best matching asset for the given backend, platform, and architecture.
    /// Uses a scoring system to pick the most specific match.
    /// </summary>
    private static GithubAsset? FindMatchingAsset(IEnumerable<GithubAsset> assets, BackendType backend, string platform, string arch)
    {
        var binaryName = GetBinaryName(backend);

        // Define which asset name patterns to search for based on backend
        var patterns = GetAssetPatterns(backend);

        var candidates = new List<(GithubAsset Asset, int Score)>();

        foreach (var asset in assets)
        {
            var name = asset.Name.ToLowerInvariant();
            var score = 0;

            // Check platform match
            var hasPlatform = false;
            if (platform == "windows" && (name.Contains("win") || name.Contains("windows")))
            {
                hasPlatform = true;
                score += 3;
            }
            else if (platform == "linux" && (name.Contains("ubuntu") || name.Contains("linux")))
            {
                hasPlatform = true;
                score += 3;
            }
            else if (platform == "macos" && name.Contains("macos"))
            {
                hasPlatform = true;
                score += 3;
            }

            if (!hasPlatform)
                continue;

            // Check architecture match
            var hasArch = false;
            if (arch == "x64" && (name.Contains("x64") || name.Contains("x86_64") || name.Contains("x86")))
            {
                hasArch = true;
                score += 3;
            }
            else if (arch == "arm64" && (name.Contains("arm64") || name.Contains("aarch64")))
            {
                hasArch = true;
                score += 3;
            }

            if (!hasArch)
                continue;

            // Check backend-specific patterns
            foreach (var pattern in patterns)
            {
                if (name.Contains(pattern.ToLowerInvariant()))
                {
                    score += 5;
                    break;
                }
            }

            // Prefer assets that contain the binary name pattern
            if (name.Contains(binaryName.ToLowerInvariant()))
                score += 2;

            candidates.Add((asset, score));
        }

        return candidates.OrderByDescending(c => c.Score).FirstOrDefault().Asset;
    }

    /// <summary>
    /// Gets the patterns to search for in asset names for a given backend.
    /// </summary>
    private static string[] GetAssetPatterns(BackendType backend) => backend switch
    {
        BackendType.Cpu => new[] { "cpu", "bin-cpu" },
        BackendType.Cuda => new[] { "cuda", "cudart" },
        BackendType.Metal => new[] { "metal", "kleidiai" },
        BackendType.Vulkan => new[] { "vulkan", "vk" },
        _ => new[] { "cpu" }
    };

    /// <summary>
    /// Gets the binary name for a given backend.
    /// </summary>
    private static string GetBinaryName(BackendType backend) => backend switch
    {
        BackendType.Cpu => "llama-server",
        BackendType.Cuda => "llama-server-cuda",
        BackendType.Metal => "llama-server-metal",
        BackendType.Vulkan => "llama-server-vulkan",
        _ => "llama-server"
    };

    /// <summary>
    /// Determines the archive type from the filename.
    /// </summary>
    private static ArchiveType GetArchiveType(string name)
    {
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return ArchiveType.Zip;
        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            return ArchiveType.TarGz;
        return ArchiveType.Unknown;
    }

    /// <summary>
    /// Extracts a ZIP archive and locates the binary inside.
    /// </summary>
    private static async Task ExtractZipAsync(string archivePath, string extractDir, string targetPath, BackendType backend)
    {
        // Use PowerShell to extract the ZIP (cross-platform compatible)
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -Command \"Expand-Archive -Path '{archivePath}' -DestinationPath '{extractDir}' -Force\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start PowerShell for ZIP extraction");

        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"ZIP extraction failed with exit code {process.ExitCode}: {error}");
        }

        // Find the binary in the extracted files
        var extractedBinary = FindExtractedBinary(extractDir, backend);
        if (string.IsNullOrEmpty(extractedBinary))
            throw new InvalidOperationException($"Could not find extracted binary in {extractDir}");

        File.Move(extractedBinary, targetPath, overwrite: true);
    }

    /// <summary>
    /// Extracts a TAR.GZ archive and locates the binary inside.
    /// </summary>
    private static async Task ExtractTarGzAsync(string archivePath, string extractDir, string targetPath, BackendType backend)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (isWindows)
        {
            // Use PowerShell's tar support (available in PowerShell 7+)
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"tar -xzf '{archivePath}' -C '{extractDir}'\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start PowerShell for TAR extraction");

            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"TAR extraction failed with exit code {process.ExitCode}: {error}");
            }
        }
        else
        {
            // Native tar on Linux/macOS
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{archivePath}\" -C \"{extractDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start tar for extraction");

            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"TAR extraction failed with exit code {process.ExitCode}: {error}");
            }
        }

        // Find the binary in the extracted files
        var extractedBinary = FindExtractedBinary(extractDir, backend);
        if (string.IsNullOrEmpty(extractedBinary))
            throw new InvalidOperationException($"Could not find extracted binary in {extractDir}");

        File.Move(extractedBinary, targetPath, overwrite: true);
    }

    /// <summary>
    /// Finds the extracted binary in the given directory.
    /// Searches for llama-server, llama-server-cuda, etc.
    /// </summary>
    private static string? FindExtractedBinary(string directory, BackendType backend)
    {
        var binaryName = GetBinaryName(backend);
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var extension = isWindows ? ".exe" : "";

        // Search recursively for the binary
        var searchPatterns = new[]
        {
            $"{binaryName}{extension}",
            $"llama-server{extension}",
            $"llama-server-{backend.ToString().ToLower()}{extension}",
        };

        foreach (var pattern in searchPatterns)
        {
            var found = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            if (found.Length > 0)
                return found[0];
        }

        // Fallback: find any executable-like file
        var extensions = isWindows ? new[] { ".exe" } : Array.Empty<string>();
        foreach (var ext in extensions)
        {
            var found = Directory.GetFiles(directory, $"*{ext}", SearchOption.TopDirectoryOnly)
                .Where(f => f.Contains("llama-server"));
            if (found.Any())
                return found.First();
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task<bool> ValidateBinaryLocallyAsync(string path, BackendType backend)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(processInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync().ConfigureAwait(false);
            var success = process.ExitCode == 0;

            if (!success)
            {
                var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                _logger?.LogWarning("Binary validation failed for {Backend}: exit code {Code}, stderr: {StdErr}, stdout: {StdOut}",
                    backend, process.ExitCode, stderr, stdout);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Binary validation threw exception for {Backend}", backend);
            return false;
        }
    }

    private bool ValidateBinaryLocally(string path, BackendType backend)
    {
        var task = ValidateBinaryLocallyAsync(path, backend);
        task.Wait(TimeSpan.FromSeconds(5));
        return task.Result;
    }

    private void SetExecutablePermission(string path)
    {
        try
        {
            var platform = GetPlatform();
            if (platform is "linux" or "macos")
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(processInfo)?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — the binary may still work
            _logger?.LogWarning(ex, "Failed to set executable permission on {Path}", path);
        }
    }

    private static void CleanupTempFiles(ILogger? logger = null, params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                // Non-fatal — log and continue
                logger?.LogWarning(ex, "Failed to clean up temp file: {File}", path);
            }
        }
    }

    private static string GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macos";
        return "linux";
    }

    private static string GetArchitecture()
    {
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        return arch switch
        {
            "x64" => "x64",
            "arm64" => "arm64",
            _ => arch
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}

internal enum ArchiveType
{
    Unknown,
    Zip,
    TarGz
}

internal record GithubAsset(
    string Name,
    string BrowserDownloadUrl,
    string? Checksum);

internal record GithubRelease(string TagName, List<GithubAsset> Assets);