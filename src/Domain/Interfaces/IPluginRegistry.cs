using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenLMStudio.Domain.Interfaces;

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
    /// Checks for available updates for installed plugins against the registry.
    /// </summary>
    Task<IEnumerable<PluginUpdateInfo>> GetAvailableUpdatesAsync();
}

/// <summary>
/// Metadata definition of a plugin (from registry or installed).
/// </summary>
public record PluginDefinition(
    string Id,
    string Name,
    string Description,
    Version Version,
    Version RegistryVersion,  // Latest version available in the registry
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