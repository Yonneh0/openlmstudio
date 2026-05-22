using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Supported backend types for engine binaries.
/// </summary>
public enum BackendType
{
    Cpu,
    Cuda,
    Metal,
    Vulkan
}

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
/// </summary>
public class EngineBinaryDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EngineBinaryDownloader> _logger;
    private readonly string _cacheDirectory;
    private readonly object _lock = new();
    private bool _disposed;

    public static readonly string DefaultVersion = "main";
    public static readonly string GitHubRepo = "ggerganov/llama.cpp";
    public static readonly TimeSpan ReleaseApiTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    public EngineBinaryDownloader(ILogger<EngineBinaryDownloader>? logger = null, string? cacheDirectory = null)
    {
        _logger = logger;
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines");
        _httpClient = new HttpClient { Timeout = DownloadTimeout };
    }

    /// <summary>
    /// Downloads the engine binary for the specified backend and returns the local path.
    /// Verifies SHA256 checksum if available in the release.
    /// Validates binary by running --version on Windows or --help on Unix.
    /// </summary>
    public async Task<string> DownloadForBackendAsync(BackendType backend, EngineDownloadConfig? config = null)
    {
        config ??= new EngineDownloadConfig(DefaultVersion);
        var version = config.Version;
        var platform = GetPlatform();
        var arch = GetArchitecture();
        var binaryName = GetBinaryName(backend);

        var cachePath = Path.Combine(_cacheDirectory, backend.ToString().ToLower(), $"{binaryName}-{platform}-{arch}");

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
        var asset = releaseInfo.Assets
            .FirstOrDefault(a => a.Name.Contains(binaryName) &&
                                 a.Name.Contains(platform) &&
                                 a.Name.Contains(arch));

        if (asset == null)
            throw new InvalidOperationException($"No matching binary found for {backend} on {platform}-{arch}");

        // Download the binary
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var tempPath = cachePath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var fileStream = File.Create(tempPath);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);

            // Verify checksum if available
            if (!string.IsNullOrEmpty(asset.Checksum))
            {
                var actualChecksum = await ComputeSha256Async(tempPath).ConfigureAwait(false);
                if (!string.Equals(actualChecksum, asset.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempPath);
                    throw new InvalidOperationException(
                        $"Checksum verification failed for {backend} binary. Expected {asset.Checksum}, got {actualChecksum}");
                }
                _logger.LogDebug("Checksum verified for {Backend}", backend);
            }
            else if (!string.IsNullOrEmpty(config.ChecksumOverride))
            {
                var actualChecksum = await ComputeSha256Async(tempPath).ConfigureAwait(false);
                if (!string.Equals(actualChecksum, config.ChecksumOverride, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempPath);
                    throw new InvalidOperationException(
                        $"Checksum verification failed for {backend} binary. Expected {config.ChecksumOverride}, got {actualChecksum}");
                }
            }

            File.Move(tempPath, cachePath, overwrite: true);

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
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Checks for new engine releases and returns version info if an update is available.
    /// </summary>
    public async Task<EngineBinaryInfo?> CheckForUpdateAsync(BackendType backend)
    {
        var releaseInfo = await GetReleaseAsync(DefaultVersion).ConfigureAwait(false);
        if (releaseInfo == null)
            return null;

        var platform = GetPlatform();
        var arch = GetArchitecture();
        var binaryName = GetBinaryName(backend);

        var asset = releaseInfo.Assets
            .FirstOrDefault(a => a.Name.Contains(binaryName) &&
                                 a.Name.Contains(platform) &&
                                 a.Name.Contains(arch));

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
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up stale temp files");
            }
        }
    }

    private async Task<GithubRelease?> GetReleaseAsync(string version)
    {
        try
        {
            var url = string.IsNullOrEmpty(version) || version == DefaultVersion
                ? "https://api.github.com/repos/ggerganov/llama.cpp/releases/latest"
                : $"https://api.github.com/repos/ggerganov/llama.cpp/releases/tags/{version}";

            using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
            var root = doc.RootElement;

            var assets = root.GetProperty("assets").EnumerateArray()
                .Select(a => new GithubAsset(
                    a.GetProperty("name").GetString() ?? "",
                    a.GetProperty("browser_download_url").GetString() ?? "",
                    GetChecksumFromName(a.GetProperty("name").GetString() ?? "")))
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

    private static string? GetChecksumFromName(string name)
    {
        // Extract SHA256 from filenames like: llama-server-linux-x64-87bba9d8a1b0.gz
        var shaMatch = System.Text.RegularExpressions.Regex.Match(name, @"([a-f0-9]{40})\.gz$");
        return shaMatch.Success ? shaMatch.Groups[1].Value : null;
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static bool ValidateBinaryLocally(string path, BackendType backend)
    {
        try
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var processInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = isWindows ? "--version" : "--help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(processInfo);
            if (process == null)
                return false;

            process.WaitForExit(5000);
            var success = process.ExitCode == 0;

            if (!success)
            {
                var stderr = process.StandardError.ReadToEnd();
                _logger.LogWarning("Binary validation failed for {Backend}: exit code {Code}, stderr: {StdErr}",
                    backend, process.ExitCode, stderr);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Binary validation threw exception for {Backend}", backend);
            return false;
        }
    }

    private static void SetExecutablePermission(string path)
    {
        try
        {
            var platform = GetPlatform();
            if (platform is "linux" or "macos")
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = "+x " + path,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(processInfo)?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — the binary may still work
            // _logger?.LogWarning(ex, "Failed to set executable permission on {Path}", path);
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

    private static string GetBinaryName(BackendType backend) => backend switch
    {
        BackendType.Cuda => "llama-server-cuda",
        BackendType.Metal => "llama-server-metal",
        BackendType.Vulkan => "llama-server-vulkan",
        BackendType.Cpu => "llama-server",
        _ => "llama-server"
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}

internal record GithubAsset(
    string Name,
    string BrowserDownloadUrl,
    string? Checksum);

internal record GithubRelease(string TagName, List<GithubAsset> Assets);
