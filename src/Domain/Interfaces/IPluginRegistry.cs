using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenLMStudio.Domain.Interfaces;

/// <summary>
/// Defines the sandboxing policy for plugin execution.
/// </summary>
public record PluginSandboxPolicy(
    bool AllowFileWrites,
    bool AllowNetworkAccess,
    bool AllowCommandExecution,
    HashSet<string> AllowedPaths,      // Paths the plugin can access (whitelist)
    HashSet<string> BlockedCommands,   // Commands the plugin cannot execute
    TimeSpan MaxExecutionTime,         // Maximum time a plugin task may run
    int MaxMemoryMb                    // Memory limit for the plugin process in MB
);

/// <summary>
/// Manages plugin discovery, installation, and lifecycle.
/// </summary>
public interface IPluginRegistry : IDisposable
{
    /// <summary>
    /// Discovers all installed plugins on the system.
    /// </summary>
    Task<IEnumerable<PluginDefinition>> ListInstalledPluginsAsync();

    /// <summary>
    /// Searches the plugin registry for available plugins matching a query term.
    /// </summary>
    Task<IEnumerable<PluginDefinition>> SearchRegistryAsync(string query);

    /// <summary>
    /// Installs a plugin from the registry or a local file path.
    /// </summary>
    Task InstallPluginAsync(PluginDefinition plugin, CancellationToken ct = default);

    /// <summary>
    /// Uninstalls an installed plugin by its ID.
    /// </summary>
    Task UninstallPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables a plugin without uninstalling it.
    /// </summary>
    Task SetEnabledStateAsync(string pluginId, bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Sets the URL of the public plugin registry. Null means no remote registry configured.
    /// </summary>
    void SetRegistryUrl(Uri? registryUrl);

    /// <summary>
    /// Returns the currently configured plugin registry URL, or null if not set.
    /// </summary>
    Uri? GetRegistryUrl();

    /// <summary>
    /// Checks for available updates for installed plugins against the registry.
    /// </summary>
    Task<IEnumerable<PluginUpdateInfo>> GetAvailableUpdatesAsync();

    /// <summary>
    /// Sets the sandbox policy for a plugin by ID.
    /// </summary>
    Task SetSandboxPolicyAsync(string pluginId, PluginSandboxPolicy policy);

    /// <summary>
    /// Gets the sandbox policy for a plugin, returning the default if none set.
    /// </summary>
    Task<PluginSandboxPolicy> GetSandboxPolicyAsync(string pluginId);

    /// <summary>
    /// Fetches full details of a single plugin from the remote registry (includes download URL).
    /// Returns null if the plugin is not found in the registry.
    /// </summary>
    Task<PluginDefinition?> GetPluginFromRegistryAsync(string pluginId);

    /// <summary>
    /// Lists all available plugins from the remote registry catalog (including download URLs for installation).
    /// </summary>
    Task<IEnumerable<PluginDefinition>> ListRegistryPluginsAsync();
}

/// <summary>
/// Metadata definition of a plugin (from registry or installed).
/// </summary>
public record PluginDefinition(
    string Id,
    string Name,
    string Description,
    Version Version,
    Version? RegistryVersion,  // Latest version available in the registry (null for local-only plugins)
    bool IsInstalled,
    bool IsEnabled,
    List<string> Tags,
    string Author,
    Uri? DownloadUrl
);

/// <summary>
/// Information about an available plugin update.
/// </summary>
public record PluginUpdateInfo(
    string PluginId,
    Version InstalledVersion,
    Version AvailableVersion,
    bool IsSecurityUpdate
);

/// <summary>
/// Default sandbox policy applied to plugins that have no custom policy configured.
/// </summary>
public static class PluginSandboxPolicyDefaults
{
    public static readonly PluginSandboxPolicy Default = new(
        AllowFileWrites: false,
        AllowNetworkAccess: false,
        AllowCommandExecution: false,
        AllowedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/tmp", "/var/tmp" },
        BlockedCommands: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "sudo", "su", "chmod", "chown", "rm -rf", "dd", "mkfs", "fdisk", "iptables" },
        MaxExecutionTime: TimeSpan.FromMinutes(5),
        MaxMemoryMb: 256
    );
}
