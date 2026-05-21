using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// JSON-based window state persistence using the AppDataDirectoryResolver.
/// </summary>
public class WindowSettingsService : IWindowSettings, IDisposable
{
    private readonly ILogger<WindowSettingsService>? _logger;
    private readonly AppDataDirectoryResolver _resolver;
    private const string SettingsFileName = "window_state.json";

    public WindowSettingsService(
        ILogger<WindowSettingsService>? logger,
        AppDataDirectoryResolver resolver)
    {
        _logger = logger;
        _resolver = resolver;
    }

    private string SettingsFilePath => Path.Combine(_resolver.MetadataDirectory, SettingsFileName);

    public async Task SaveAsync(WindowStateSettings settings)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingsFilePath, json);
            _logger?.LogDebug("Window state saved to {Path}", SettingsFilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save window state");
        }
    }

    public async Task<WindowStateSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                _logger?.LogDebug("No window state file found at {Path}", SettingsFilePath);
                return new WindowStateSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<WindowStateSettings>(json);
            _logger?.LogDebug("Window state loaded from {Path}", SettingsFilePath);
            return settings ?? new WindowStateSettings();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load window state, using defaults");
            return new WindowStateSettings();
        }
    }

    public void Dispose()
    {
        // No unmanaged resources
    }
}