using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Information about an available application update.
/// </summary>
public record AppUpdateInfo(
    string Version,
    string Notes,
    string DownloadUrl,
    DateTime ReleasedAt);

/// <summary>
/// Checks for application updates via GitHub Releases API.
/// </summary>
public interface IAppUpdateChecker
{
    Task<AppUpdateInfo?> CheckForUpdatesAsync();
}

/// <summary>
/// App update checker that caches results for 1 hour.
/// </summary>
public class AppUpdateChecker : IAppUpdateChecker, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppUpdateChecker> _logger;
    private readonly string _cacheFile;
    private readonly object _lock = new();
    private AppUpdateInfo? _cachedUpdate;
    private DateTime _cacheExpiry;
    private bool _disposed;

    public AppUpdateChecker(ILogger<AppUpdateChecker> logger, string? cacheFile = null)
    {
        _logger = logger;
        _cacheFile = cacheFile ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "update-cache.json");
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<AppUpdateInfo?> CheckForUpdatesAsync()
    {
        // Check cache first (1 hour expiry)
        lock (_lock)
        {
            if (_cachedUpdate != null && DateTime.UtcNow < _cacheExpiry)
                return _cachedUpdate;
        }

        try
        {
            var response = await _httpClient.GetAsync(
                "https://api.github.com/repos/Yonneh0/openlmstudio/releases/latest").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode} for releases", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GithubReleaseInfo>(json);

            if (release == null)
                return null;

            var currentVersion = GetCurrentVersion();
            if (CompareVersions(currentVersion, release.TagName) >= 0)
            {
                _logger.LogDebug("No update available: current={Current}, latest={Latest}", currentVersion, release.TagName);
                return null;
            }

            var updateInfo = new AppUpdateInfo(
                Version: release.TagName,
                Notes: release.Body ?? "No release notes",
                DownloadUrl: GetDownloadUrl(release),
                ReleasedAt: release.PublishedAt);

            // Cache the result
            lock (_lock)
            {
                _cachedUpdate = updateInfo;
                _cacheExpiry = DateTime.UtcNow.AddHours(1);
            }

            _logger.LogInformation("Update available: {Current} -> {Latest}", currentVersion, release.TagName);
            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
            return null;
        }
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "0.0.0.0";
    }

    private static int CompareVersions(string current, string latest)
    {
        // Strip 'v' prefix if present
        var cleanCurrent = current.TrimStart('v');
        var cleanLatest = latest.TrimStart('v');

        var currentParts = cleanCurrent.Split('.').Select(int.Parse).ToArray();
        var latestParts = cleanLatest.Split('.').Select(int.Parse).ToArray();

        for (var i = 0; i < Math.Max(currentParts.Length, latestParts.Length); i++)
        {
            var cur = i < currentParts.Length ? currentParts[i] : 0;
            var lat = i < latestParts.Length ? latestParts[i] : 0;

            if (cur < lat) return -1;
            if (cur > lat) return 1;
        }

        return 0;
    }

    private static string GetDownloadUrl(GithubReleaseInfo release)
    {
        // Use the first asset's download URL, or the release page
        if (release.Assets != null && release.Assets.Count > 0)
            return release.Assets[0].BrowserDownloadUrl;

        return release.HtmlUrl ?? $"https://github.com/Yonneh0/openlmstudio/releases/tag/{release.TagName}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}

internal record GithubReleaseInfo(
    string TagName,
    string Body,
    string? HtmlUrl,
    DateTime PublishedAt,
    List<GithubAssetInfo>? Assets);

internal record GithubAssetInfo(
    string Name,
    string BrowserDownloadUrl);