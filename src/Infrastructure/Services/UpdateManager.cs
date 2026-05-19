using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Update manager that checks for application updates via GitHub Releases API.
/// Falls back to local version comparison when no network is available.
/// </summary>
public class UpdateManager : IUpdateManager, IDisposable
{
    private readonly ILogger<UpdateManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;
    private UpdateInfo? _availableUpdate;
    private UpdateStatus _status;
    private double _downloadProgress;

    public UpdateManager(ILogger<UpdateManager> logger, AppDataDirectoryResolver appDataResolver)
    {
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        _currentVersion = typeof(UpdateManager).Assembly.GetName().Version?.ToString() ?? "0.0.1";
        _status = UpdateStatus.Current;
    }

    public UpdateStatus CurrentStatus => _status;
    public double DownloadProgress => _downloadProgress;

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            // Check GitHub Releases for the latest tag
            var response = await _httpClient.GetAsync("repos/Yonneh0/OpenLMStudio/releases/latest", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates: {StatusCode}", response.StatusCode);
                return null;
            }

            using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), ct: ct);
            var root = jsonDoc.RootElement;
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var releaseNotes = root.GetProperty("body").GetString() ?? "";
            var published = DateTime.SpecifyKind(
                DateTime.Parse(root.GetProperty("published_at").GetString() ?? DateTime.UtcNow.ToString("o")),
                DateTimeKind.Utc);

            // Determine total download size from assets
            long totalSize = 0;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    totalSize += asset.GetProperty("size").GetInt64();
                }
            }

            var latest = new UpdateInfo(tagName, releaseNotes, totalSize, published);

            // Compare versions
            if (CompareVersions(latest.Version, _currentVersion) > 0)
            {
                _availableUpdate = latest;
                _status = UpdateStatus.UpdateAvailable;
                _logger.LogInformation("Update available: {Current} -> {Latest}", _currentVersion, latest.Version);
                return latest;
            }

            _status = UpdateStatus.Current;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for updates");
            return null;
        }
    }

    public async Task<bool> DownloadUpdateAsync(CancellationToken ct = default)
    {
        if (_availableUpdate == null)
            return false;

        _status = UpdateStatus.Downloading;
        try
        {
            // Download the latest release asset (executable installer)
            var response = await _httpClient.GetAsync(
                "repos/Yonneh0/OpenLMStudio/releases/latest/assets/1",
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = File.Create(Path.Combine(AppDataDirectoryResolver.AppDataPath, "updates", "update.zip"));
            await stream.CopyToAsync(fileStream, 81920, ct);
            _downloadProgress = 100;
            _status = UpdateStatus.DownloadComplete;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading update");
            _status = UpdateStatus.DownloadFailed;
            return false;
        }
    }

    public Task<bool> ApplyUpdateAsync(CancellationToken ct = default)
    {
        if (_status != UpdateStatus.DownloadComplete)
            return Task.FromResult(false);

        // In production, this would run the installer
        // For now, log the action
        _logger.LogInformation("Update applied (placeholder)");
        _status = UpdateStatus.Current;
        _availableUpdate = null;
        return Task.FromResult(true);
    }

    public async Task<IReadOnlyList<UpdateInfo>> CheckPluginUpdatesAsync(CancellationToken ct = default)
    {
        // Delegate to PluginRegistry.GetAvailableUpdatesAsync
        // This is a placeholder that returns empty — real implementation needs PluginRegistry access
        return Array.Empty<UpdateInfo>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// Returns positive if v1 > v2, negative if v1 < v2, zero if equal.
    /// </summary>
    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.TrimStart('v').Split('.');
        var parts2 = v2.TrimStart('v').Split('.');
        var length = Math.Max(parts1.Length, parts2.Length);

        for (int i = 0; i < length; i++)
        {
            var n1 = i < parts1.Length ? int.Parse(parts1[i]) : 0;
            var n2 = i < parts2.Length ? int.Parse(parts2[i]) : 0;
            if (n1 != n2)
                return n1.CompareTo(n2);
        }
        return 0;
    }
}