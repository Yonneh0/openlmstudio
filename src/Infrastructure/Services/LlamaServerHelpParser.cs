using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models.LLamaCpp;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Represents a parsed setting from llama-server --help output.
/// </summary>
public record HelpSetting(
    string Name,           // e.g. "--ctx-size"
    string? Description,   // e.g. "Text context, number of tokens"
    string? DefaultValue,  // e.g. "512"
    string? Category       // e.g. "Context"
);

/// <summary>
/// Parses `llama-server --help` output into structured settings.
/// Auto-discovers all available settings without hard-coding them.
/// </summary>
public class LlamaServerHelpParser
{
    private readonly ILogger<LlamaServerHelpParser> _logger;
    private readonly object _lock = new();
    private HelpSetting[]? _cachedSettings;
    private DateTime _cacheExpiry;

    public LlamaServerHelpParser(ILogger<LlamaServerHelpParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes `llama-server --help` (or `-h`) and parses the output.
    /// Uses a 5-minute cache to avoid repeated parsing.
    /// </summary>
    public async Task<HelpSetting[]> GetSettingsAsync(string? binaryPath = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_cachedSettings is not null && DateTime.UtcNow < _cacheExpiry)
                return _cachedSettings;
        }

        var settings = await ParseHelpOutputAsync(binaryPath, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _cachedSettings = settings;
            _cacheExpiry = DateTime.UtcNow.AddMinutes(5);
        }

        return settings;
    }

    /// <summary>
    /// Clears the cached settings (e.g., after a binary change).
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _cachedSettings = null;
            _cacheExpiry = DateTime.MinValue;
        }
    }

    private async Task<HelpSetting[]> ParseHelpOutputAsync(string? binaryPath, CancellationToken ct)
    {
        var path = binaryPath ?? FindLlamaServer();
        if (string.IsNullOrEmpty(path))
            return Array.Empty<HelpSetting>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return Array.Empty<HelpSetting>();

            var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            return ParseHelpText(output);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse llama-server --help from {Path}", path);
            return Array.Empty<HelpSetting>();
        }
    }

    /// <summary>
    /// Parses llama-server --help output into structured settings.
    /// Supports formats like:
    ///   --ctx-size, -c        Text context, number of tokens (default: 512)
    ///   --ngl, --gpu-layers   Number of layers to offload to GPU (default: auto)
    /// </summary>
    private static HelpSetting[] ParseHelpText(string text)
    {
        var settings = new List<HelpSetting>();
        var lines = text.Split('\n');

        // Category detection from section headers
        var currentCategory = "General";
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Detect section headers (usually in ALL CAPS or followed by ---)
            if (line.EndsWith("OPTIONS") || line.EndsWith("OPTIONS:"))
            {
                currentCategory = line.Replace("OPTIONS", "").Replace(":", "").Trim();
                continue;
            }

            // Parse setting lines: "--flag, --flag2  description (default: value)"
            var match = System.Text.RegularExpressions.Regex.Match(line,
                @"^\s*(--[\w-]+)(?:,\s*(--[\w-]+))?\s+(.+?)(?:\s*\(default:\s*(.+?)\))?\s*$");

            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                var altName = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
                var description = match.Groups[3].Value.Trim();
                var defaultValue = match.Groups[4].Success ? match.Groups[4].Value.Trim() : null;

                if (!string.IsNullOrEmpty(description))
                {
                    settings.Add(new HelpSetting(name, description, defaultValue, currentCategory));
                    if (altName != null)
                    {
                        settings.Add(new HelpSetting(altName, description, defaultValue, currentCategory));
                    }
                }
            }
        }

        return settings.ToArray();
    }

    private static string FindLlamaServer()
    {
        // Check common paths
        var paths = new[]
        {
            "llama-server",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenLMStudio", "engines", "cpu", "llama-server"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenLMStudio", "engines", "cpu", "llama-server-cpu"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenLMStudio", "engines", "cuda", "llama-server-cuda"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenLMStudio", "engines", "metal", "llama-server-metal"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenLMStudio", "engines", "vulkan", "llama-server-vulkan"),
        };

        foreach (var p in paths)
        {
            if (File.Exists(p))
                return p;
        }

        return "llama-server"; // Fallback to PATH
    }
}