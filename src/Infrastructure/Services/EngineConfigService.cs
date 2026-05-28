using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Configuration for the engine (llama.cpp server settings).
/// </summary>
public record EngineConfig(
    string ModelPath,
    int Port,
    float Temperature,
    float TopP,
    string RecommendedBackend,
    string? LastDownloadedBackend);

/// <summary>
/// Persists and loads engine configuration to/from disk.
/// </summary>
public interface IEngineConfigService
{
    Task<EngineConfig> LoadAsync();
    Task SaveAsync(EngineConfig config);
}

/// <summary>
/// Engine configuration service with auto-migration for old formats.
/// </summary>
public class EngineConfigService : IEngineConfigService
{
    private readonly ILogger<EngineConfigService> _logger;
    private readonly string _configPath;

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public EngineConfigService(ILogger<EngineConfigService> logger, string? configPath = null)
    {
        _logger = logger;
        _configPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "config.json");
    }

    public async Task<EngineConfig> LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogDebug("Config file not found, returning defaults");
            return CreateDefaultConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<EngineConfig>(json);

            if (config == null)
            {
                _logger.LogWarning("Config file is empty or invalid, using defaults");
                return CreateDefaultConfig();
            }

            // Auto-migrate old formats
            config = MigrateConfig(config);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load config, using defaults");
            return CreateDefaultConfig();
        }
    }

    public async Task SaveAsync(EngineConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(config, _jsonOptions);

            // Write to temp file first, then rename for atomicity
            var tempPath = _configPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, _configPath, overwrite: true);

            _logger.LogDebug("Saved engine config to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save engine config");
            throw;
        }
    }

    private static EngineConfig CreateDefaultConfig()
    {
        return new EngineConfig(
            ModelPath: "",
            Port: 8081,
            Temperature: 0.7f,
            TopP: 0.9f,
            RecommendedBackend: "cpu",
            LastDownloadedBackend: null);
    }

    private EngineConfig MigrateConfig(EngineConfig config)
    {
        // Migration: if Port is 0 (old default), set to 8081
        if (config.Port == 0)
        {
            _logger.LogInformation("Migrating config: resetting port to 8081");
            return config with { Port = 8081 };
        }

        // Migration: if RecommendedBackend is empty, set to cpu
        if (string.IsNullOrEmpty(config.RecommendedBackend))
        {
            _logger.LogInformation("Migrating config: setting recommended backend to cpu");
            return config with { RecommendedBackend = "cpu" };
        }

        return config;
    }
}