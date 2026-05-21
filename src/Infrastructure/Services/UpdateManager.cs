using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Interfaces;

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
    private readonly AppDataDirectoryResolver _appDataResolver;
    private readonly IPluginRegistry? _pluginRegistry;
    private UpdateInfo? _availableUpdate;
    private UpdateStatus _status;
    private double _downloadProgress;

    public UpdateManager(ILogger<UpdateManager> logger, AppDataDirectoryResolver appDataResolver, IPluginRegistry? pluginRegistry = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        _appDataResolver = appDataResolver;
        _pluginRegistry = pluginRegistry;
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

            using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
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
            var appDataDir = _appDataResolver.GetAppDataDirectory();
            var updatesDir = Path.Combine(appDataDir, "updates");
            Directory.CreateDirectory(updatesDir);
            using var fileStream = File.Create(Path.Combine(updatesDir, "update.zip"));
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

    public async Task<bool> ApplyUpdateAsync(CancellationToken ct = default)
    {
        if (_status != UpdateStatus.DownloadComplete)
            return false;

        var appDataDir = _appDataResolver.GetAppDataDirectory();
        var updatesDir = Path.Combine(appDataDir, "updates");
        var updateZip = Path.Combine(updatesDir, "update.zip");
        if (!File.Exists(updateZip))
        {
            _logger.LogError("Update file not found: {Path}", updateZip);
            _status = UpdateStatus.DownloadFailed;
            return false;
        }

        if (_availableUpdate == null)
        {
            _status = UpdateStatus.Current;
            return false;
        }

        try
        {
            _logger.LogInformation("Applying update {Version} from {Zip}", _availableUpdate.Version, updateZip);

            // Extract update archive to a staging directory
            var stagingDir = Path.Combine(updatesDir, $"staging-{_availableUpdate.Version}");
            Directory.CreateDirectory(stagingDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(updateZip, stagingDir, true);

            // Find the executable to replace (current app exe path)
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
            {
                _logger.LogWarning("Could not locate current executable for update replacement");
                _status = UpdateStatus.Current;
                return false;
            }

            // Write a small launcher script that will swap files and restart
            var launcherScript = Path.Combine(updatesDir, $"update-launcher-{Guid.NewGuid()}.bat");
            var backupPath = currentExe + ".backup";
            var launcherContent = $"copy /y \"{stagingDir}\\*\" \"{Path.GetDirectoryName(currentExe)}\" && del /f /q \"{backupPath}\" && start \"\" \"{currentExe}\" && exit\n"
                + $"copy /y \"{currentExe}\" \"{backupPath}\"\n"
                + $"for %%f in (\"{stagingDir}\\*.dll\" \"{stagingDir}\\*.exe\" \"{stagingDir}\\*.pdb\") do copy /y \"%%f\" \"{Path.GetDirectoryName(currentExe)}\"\n"
                + $"timeout /t 2 >nul\n"
                + $"start \"\" \"{currentExe}\"\n"
                + $"exit";
            await File.WriteAllTextAsync(launcherScript, launcherContent);

            // Launch the update script in background and exit gracefully
            _logger.LogInformation("Launching update script");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"pushd {updatesDir} && {Path.GetFileName(launcherScript)}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            _status = UpdateStatus.Applying;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying update");
            _status = UpdateStatus.DownloadFailed;
            return false;
        }
    }

    public async Task<IReadOnlyList<UpdateInfo>> CheckPluginUpdatesAsync(CancellationToken ct = default)
    {
        if (_pluginRegistry == null)
        {
            _logger.LogDebug("IPluginRegistry not available for plugin update checks");
            return Array.Empty<UpdateInfo>();
        }

        var updates = await _pluginRegistry.GetAvailableUpdatesAsync() ?? Array.Empty<Domain.Interfaces.PluginUpdateInfo>();
        return updates.Select(u => new UpdateInfo(u.AvailableVersion.ToString(), $"{u.PluginId}: {u.InstalledVersion} → {u.AvailableVersion}", 0, DateTime.UtcNow)).ToList();
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