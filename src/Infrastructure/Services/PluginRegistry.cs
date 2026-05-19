using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of the plugin registry that handles local discovery, installation, and updates.
/// Supports remote registry integration via SetRegistryUrl(), sandbox policy enforcement for plugins,
/// and local manifest-based metadata management.
/// </summary>
public class PluginRegistry : Domain.Interfaces.IPluginRegistry
{
    private readonly ILogger<PluginRegistry>? _logger;
    private HttpClient? _httpClient;  // Lazy initialization since logger may not be available at construction time
    private readonly string _pluginDirectory;  // Path to the plugins/ subdirectory under appdata

    /// <summary>
    /// The URL of the public plugin registry. Null means no remote registry configured.
    /// </summary>
#pragma warning disable CS0649 // Field is never assigned to — set via property setter or method
    private Uri? _registryUrl = null;
#pragma warning restore CS0649

    /// <summary>
    /// In-memory cache of sandbox policies per plugin (persisted as JSON).
    /// </summary>
    private readonly Dictionary<string, Domain.Interfaces.PluginSandboxPolicy> _sandboxPolicies = new(StringComparer.OrdinalIgnoreCase);

    public PluginRegistry(ILogger<PluginRegistry>? logger, string pluginDirectory)
    {
        _logger = logger;
        _pluginDirectory = pluginDirectory;

        // Ensure the plugins directory exists on startup
        if (!Directory.Exists(_pluginDirectory))
            Directory.CreateDirectory(_pluginDirectory);

        // Load sandbox policies from disk (persisted in plugin settings store)
        LoadSandboxPolicies();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    /// <inheritdoc />
    public void SetRegistryUrl(Uri? registryUrl)
    {
        _registryUrl = registryUrl;

        // Create HttpClient when a new registry URL is set
        if (_registryUrl != null && _httpClient == null)
            _httpClient = CreateHttpClient();
        else if (_registryUrl == null)
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }

        _logger?.LogInformation("Plugin registry URL changed to: {Url}", registryUrl?.ToString() ?? "(none)");
    }

    /// <inheritdoc />
    public Uri? GetRegistryUrl() => _registryUrl;

    /// <inheritdoc />
    public async Task<Domain.Interfaces.PluginSandboxPolicy> GetSandboxPolicyAsync(string pluginId)
    {
        if (_sandboxPolicies.TryGetValue(pluginId, out var policy))
            return policy;

        // Return default policy for unconfigured plugins
        _logger?.LogDebug("Using default sandbox policy for plugin '{PluginId}'", pluginId);
        return Domain.Interfaces.PluginSandboxPolicyDefaults.Default;
    }

    /// <inheritdoc />
    public async Task SetSandboxPolicyAsync(string pluginId, Domain.Interfaces.PluginSandboxPolicy policy)
    {
        _sandboxPolicies[pluginId] = policy;

        // Persist the policy to disk (stored per-plugin in settings directory)
        var policyPath = Path.Combine(_pluginDirectory, pluginId, "sandbox-policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath) ?? string.Empty);
        await File.WriteAllTextAsync(policyPath, System.Text.Json.JsonSerializer.Serialize(policy));

        _logger?.LogInformation("Sandbox policy set for plugin '{PluginId}'", pluginId);
    }

    /// <summary>
    /// Loads sandbox policies from the per-plugin settings directory on disk.
    /// </summary>
    private void LoadSandboxPolicies()
    {
        foreach (var pluginDir in Directory.GetDirectories(_pluginDirectory))
        {
            var pluginId = Path.GetFileName(pluginDir);
            if (string.IsNullOrEmpty(pluginId)) continue;

            var policyPath = Path.Combine(pluginDir, "sandbox-policy.json");
            if (File.Exists(policyPath))
            {
                try
                {
                    var content = File.ReadAllText(policyPath);
                    _sandboxPolicies[pluginId] = System.Text.Json.JsonSerializer.Deserialize<Domain.Interfaces.PluginSandboxPolicy>(content)
                        ?? Domain.Interfaces.PluginSandboxPolicyDefaults.Default;
                    _logger?.LogDebug("Loaded sandbox policy for plugin '{PluginId}'", pluginId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load sandbox policy for plugin '{PluginId}'", pluginId);
                }
            }
        }

        _logger?.LogDebug("Loaded {Count} sandbox policies from disk", _sandboxPolicies.Count);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Interfaces.PluginDefinition>> ListInstalledPluginsAsync()
    {
        var result = new List<Domain.Interfaces.PluginDefinition>();

        try
        {
            // Scan each subdirectory as a potential plugin (plugin directory structure: plugins/{PluginId}/{version}/)
            foreach (var pluginDir in Directory.GetDirectories(_pluginDirectory))
            {
                var pluginName = Path.GetFileName(pluginDir);
                if (string.IsNullOrEmpty(pluginName)) continue;

                try
                {
                    // Try to load the plugin assembly and extract metadata via reflection
                    var manifestPath = Path.Combine(pluginDir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifest = await LoadPluginManifestAsync(manifestPath);
                        result.Add(new Domain.Interfaces.PluginDefinition(
                            manifest.Id ?? pluginName,
                            manifest.Name ?? pluginName,
                            manifest.Description ?? string.Empty,
                            manifest.Version ?? new Version("0.1"),
                            // No registry version for local plugins
                            null,
                            true,
                            manifest.IsEnabled ?? false,
                            manifest.Tags ?? new List<string>(),
                            manifest.Author ?? "Unknown",
                            null
                        ));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load plugin from directory: {PluginDir}", pluginDir);
                }
            }

            // Also scan for DLL-based plugins in the main plugins directory
            foreach (var dllPath in Directory.GetFiles(_pluginDirectory, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var attr = assembly.GetCustomAttribute<AssemblyPluginManifestAttribute>();
                    if (attr != null)
                    {
                        result.Add(new Domain.Interfaces.PluginDefinition(
                            attr.Id!,
                            attr.Name!,
                            attr.Description ?? string.Empty,
                            attr.Version ?? new Version("0.1"),
                            // No registry version for DLL-based plugins
                            null,
                            true,
                            attr.IsEnabled ?? false,
                            attr.Tags ?? new List<string>(),
                            attr.Author ?? "Unknown",
                            null
                        ));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load plugin DLL: {DllPath}", dllPath);
                }
            }

            _logger?.LogDebug("Discovered {Count} installed plugins", result.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error listing installed plugins");
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Interfaces.PluginDefinition>> SearchRegistryAsync(string query)
    {
        if (_registryUrl == null || _httpClient == null)
            throw new InvalidOperationException("No plugin registry configured.");

        try
        {
            // Call the remote registry API for search results
            var url = $"{_registryUrl}/api/plugins/search?q={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Registry search failed: {response.StatusCode}");

            // Deserialize the JSON response — format depends on registry implementation
            var jsonContent = await response.Content.ReadAsStringAsync();
            var definitions = System.Text.Json.JsonSerializer.Deserialize<Domain.Interfaces.PluginDefinition[]>(jsonContent);
            return definitions ?? Array.Empty<Domain.Interfaces.PluginDefinition>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching plugin registry");
            return Array.Empty<Domain.Interfaces.PluginDefinition>();
        }
    }

    /// <inheritdoc />
    public async Task<Domain.Interfaces.PluginDefinition?> GetPluginFromRegistryAsync(string pluginId)
    {
        if (_registryUrl == null || _httpClient == null)
            throw new InvalidOperationException("No plugin registry configured.");

        try
        {
            // Fetch full plugin details from the remote registry API (includes download URL, latest version, etc.)
            var url = $"{_registryUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                // Return null for missing plugins rather than throwing — allows graceful handling in UI
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                throw new HttpRequestException($"Registry plugin lookup failed: {response.StatusCode}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var definition = System.Text.Json.JsonSerializer.Deserialize<Domain.Interfaces.PluginDefinition>(jsonContent);
            return definition ?? null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching plugin '{PluginId}' from registry", pluginId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Interfaces.PluginDefinition>> ListRegistryPluginsAsync()
    {
        if (_registryUrl == null || _httpClient == null)
            throw new InvalidOperationException("No plugin registry configured.");

        try
        {
            // Fetch the full catalog of available plugins from the remote registry API
            var url = $"{_registryUrl}/api/plugins";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Registry plugin listing failed: {response.StatusCode}");

            // Deserialize the JSON response — returns a list of available plugins with download URLs
            var jsonContent = await response.Content.ReadAsStringAsync();
            var definitions = System.Text.Json.JsonSerializer.Deserialize<Domain.Interfaces.PluginDefinition[]>(jsonContent);
            return definitions ?? Array.Empty<Domain.Interfaces.PluginDefinition>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error listing registry plugins");
            return Array.Empty<Domain.Interfaces.PluginDefinition>();
        }
    }

    /// <inheritdoc />
    public async Task InstallPluginAsync(Domain.Interfaces.PluginDefinition plugin, CancellationToken ct = default)
    {
        if (plugin.IsInstalled)
            throw new InvalidOperationException($"Plugin '{plugin.Id}' is already installed.");

        var downloadUrl = plugin.DownloadUrl ?? throw new InvalidOperationException("No download URL available for plugin.");
        using var client = _httpClient ??= CreateHttpClient();
        var archiveBytes = await client.GetByteArrayAsync(downloadUrl, ct);
        var installPath = Path.Combine(_pluginDirectory, plugin.Id);

        // Extract and save the plugin to disk — assumes ZIP format
        Directory.CreateDirectory(installPath);
        using var archiveStream = new MemoryStream(archiveBytes);
        using var archiveZip = new System.IO.Compression.ZipArchive(archiveStream, System.IO.Compression.ZipArchiveMode.Read);

        foreach (var entry in archiveZip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var targetPath = Path.Combine(installPath, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);

            await using var streamWriter = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var readerStream = entry.Open();
            await readerStream.CopyToAsync(streamWriter);
        }

        // Create manifest.json for the installed plugin
        var manifestPath = Path.Combine(installPath, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Description = plugin.Description,
            Version = plugin.Version.ToString(),
            IsEnabled = true,
            Tags = plugin.Tags,
            Author = plugin.Author
        }));

        // Default sandbox policy for installed plugin
        var policyPath = Path.Combine(installPath, "sandbox-policy.json");
        await File.WriteAllTextAsync(policyPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            AllowFileWrites = false,
            AllowNetworkAccess = false,
            AllowCommandExecution = false,
            AllowedPaths = new[] { "/tmp", "/var/tmp" },
            BlockedCommands = new[] { "sudo", "su", "chmod", "chown", "rm -rf", "dd", "mkfs", "fdisk", "iptables" },
            MaxExecutionTimeSeconds = 300,
            MaxMemoryMb = 256
        }));

        _sandboxPolicies[plugin.Id] = new Domain.Interfaces.PluginSandboxPolicy(
            false, false, false,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/tmp", "/var/tmp" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sudo", "su", "chmod", "chown", "rm -rf", "dd", "mkfs", "fdisk", "iptables" },
            TimeSpan.FromMinutes(5), 256);

        _logger?.LogInformation("Installed plugin: {PluginId} from {Url}", plugin.Id, downloadUrl);
    }

    /// <inheritdoc />
    public async Task UninstallPluginAsync(string pluginId, CancellationToken ct = default)
    {
        var pluginDir = Path.Combine(_pluginDirectory, pluginId);
        if (!Directory.Exists(pluginDir))
            throw new FileNotFoundException($"Plugin directory not found: {pluginDir}");

        try
        {
            Directory.Delete(pluginDir, recursive: true);

            // Remove from settings.db (SQLite-backed)
            _logger?.LogInformation("Uninstalled plugin: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to uninstall plugin: {PluginId}", pluginId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetEnabledStateAsync(string pluginId, bool enabled, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(_pluginDirectory, pluginId, "manifest.json");
        if (File.Exists(manifestPath))
        {
            var currentManifest = await LoadPluginManifestAsync(manifestPath);

            var updateJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Id = currentManifest.Id,
                Name = currentManifest.Name,
                Description = currentManifest.Description,
                Version = (currentManifest.Version ?? new Version("0.1")).ToString(),
                IsEnabled = enabled,
                Tags = currentManifest.Tags ?? new(),
                Author = currentManifest.Author
            });

            await File.WriteAllTextAsync(manifestPath, updateJson);
            _logger?.LogInformation("Plugin '{PluginId}' state changed to: {State}", pluginId, enabled ? "Enabled" : "Disabled");
        }
        else
        {
            _logger?.LogWarning("Manifest not found for plugin '{PluginId}', cannot update enabled state", pluginId);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Domain.Interfaces.PluginUpdateInfo>> GetAvailableUpdatesAsync()
    {
        if (_registryUrl == null || _httpClient == null)
            throw new InvalidOperationException("No plugin registry configured.");

        var result = new List<Domain.Interfaces.PluginUpdateInfo>();

        // Compare installed versions against registry for each local plugin
        foreach (var installed in await ListInstalledPluginsAsync())
        {
            if (!installed.IsInstalled || installed.RegistryVersion == null) continue;

            try
            {
                // Fetch the latest version from the registry
                var url = $"{_registryUrl}/api/plugins/{Uri.EscapeDataString(installed.Id)}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var jsonContent = await response.Content.ReadAsStringAsync();
                var registryPlugin = System.Text.Json.JsonSerializer.Deserialize<Domain.Interfaces.PluginDefinition>(jsonContent);

                if (registryPlugin != null && registryPlugin.RegistryVersion > installed.RegistryVersion)
                {
                    result.Add(new Domain.Interfaces.PluginUpdateInfo(
                        installed.Id,
                        installed.Version,
                        registryPlugin.RegistryVersion,
                        false  // Could check changelog for security keywords in real implementation
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error checking update for plugin '{PluginId}': {Message}", installed.Id, ex.Message);
            }
        }

        return result;
    }

    /// <summary>
    /// Loads the manifest.json file from a plugin directory.
    /// </summary>
    private static async Task<PluginManifestData> LoadPluginManifestAsync(string manifestPath)
    {
        var content = await File.ReadAllTextAsync(manifestPath);
        return System.Text.Json.JsonSerializer.Deserialize<PluginManifestData>(content)
            ?? new PluginManifestData();
    }

    /// <summary>
    /// Creates an HttpClient for plugin registry communication.
    /// </summary>
    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5); // Allow long downloads for large plugins
        return client;
    }

    /// <summary>
    /// Temporary manifest data structure — replaced by SQLite-backed settings store.
    /// </summary>
    private class PluginManifestData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public Version? Version { get; set; }
        public bool? IsEnabled { get; set; }
        public List<string>? Tags { get; set; } = new();
        public string? Author { get; set; }
    }

    /// <summary>
    /// Custom attribute for marking assembly-level plugin manifests in DLL-based plugins.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    private class AssemblyPluginManifestAttribute : Attribute
    {
        public AssemblyPluginManifestAttribute(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
        public string? Description { get; set; }
        public Version? Version { get; set; }
        public bool? IsEnabled { get; set; }
        public List<string>? Tags { get; set; } = new();
        public string? Author { get; set; }
    }
}
