using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
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
/// Information about an engine binary release from GitHub.
/// </summary>
public record EngineBinaryInfo(
    string Version,
    string DownloadUrl,
    string? Checksum,
    string Architecture);

/// <summary>
/// Downloads and caches llama.cpp engine binaries from GitHub releases.
/// </summary>
public class EngineBinaryDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EngineBinaryDownloader> _logger;
    private readonly string _cacheDirectory;
    private readonly object _lock = new();
    private bool _disposed;

    public EngineBinaryDownloader(ILogger<EngineBinaryDownloader> logger, string? cacheDirectory = null)
    {
        _logger = logger;
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines");
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>
    /// Downloads the engine binary for the specified backend and returns the local path.
    /// </summary>
    public async Task<string> DownloadForBackendAsync(BackendType backend)
    {
        var platform = GetPlatform();
        var arch = GetArchitecture();
        var binaryName = GetBinaryName(backend);

        var cachePath = Path.Combine(_cacheDirectory, backend.ToString().ToLower(), $"{binaryName}-{platform}-{arch}");

        lock (_lock)
        {
            if (File.Exists(cachePath))
            {
                _logger.LogDebug("Found cached binary for {Backend}: {Path}", backend, cachePath);
                return cachePath;
            }
        }

        // Fetch the latest release from GitHub
        var releaseInfo = await GetLatestReleaseAsync().ConfigureAwait(false);
        if (releaseInfo == null)
            throw new InvalidOperationException("Could not fetch latest release from GitHub");

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

            File.Move(tempPath, cachePath, overwrite: true);
            _logger.LogInformation("Downloaded engine binary for {Backend} to {Path}", backend, cachePath);
            return cachePath;
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private async Task<GithubRelease?> GetLatestReleaseAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                "https://api.github.com/repos/ggerganov/llama.cpp/releases/latest").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
            var root = doc.RootElement;

            var assets = root.GetProperty("assets").EnumerateArray()
                .Select(a => new GithubAsset(
                    a.GetProperty("name").GetString() ?? "",
                    a.GetProperty("browser_download_url").GetString() ?? ""))
                .ToList();

            return new GithubRelease(
                root.GetProperty("tag_name").GetString() ?? "",
                assets);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch latest release from GitHub");
            return null;
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

internal record GithubAsset(string Name, string BrowserDownloadUrl);
internal record GithubRelease(string TagName, List<GithubAsset> Assets);