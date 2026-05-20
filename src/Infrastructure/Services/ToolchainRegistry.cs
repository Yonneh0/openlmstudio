using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;
using System.IO.Compression;
using System.Text.Json;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Registry for architecture-specific compiler toolchains.
/// Downloads and caches toolchains on demand.
/// </summary>
public class ToolchainRegistry : IToolchainRegistry, IDisposable
{
    private readonly ILogger<ToolchainRegistry> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<(ArchitectureType, string), string> _cache = new();
    private readonly string _cacheDirectory;
    private readonly object _lock = new();

    // GitHub releases base URL for pre-built toolchains
    private const string ToolchainBaseUrl = "https://github.com/openlmstudio/toolchains/releases/download";

    public ToolchainRegistry(ILogger<ToolchainRegistry> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio",
            "toolchains");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<string?> GetToolchainAsync(ArchitectureType arch, string toolName)
    {
        var key = (arch, toolName);
        if (_cache.TryGetValue(key, out var path))
            return path;

        var cacheKey = $"{arch}_{toolName}";
        var localPath = Path.Combine(_cacheDirectory, cacheKey);

        if (File.Exists(localPath))
        {
            _cache[key] = localPath;
            return localPath;
        }

        try
        {
            var url = $"{ToolchainBaseUrl}/v1.0/{cacheKey}.zip";
            var tempPath = Path.GetTempFileName();
            var response = await _httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream).ConfigureAwait(false);

            // Extract the zip to the cache directory
            Directory.CreateDirectory(localPath);
            ZipFile.ExtractToDirectory(tempPath, localPath, overwriteFiles: true);

            _cache[key] = localPath;
            _logger.LogInformation("Downloaded toolchain for {Arch}/{Tool}", arch, toolName);
            return localPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download toolchain for {Arch}/{Tool}", arch, toolName);
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}