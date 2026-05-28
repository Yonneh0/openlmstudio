using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using System.Text.Json;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages local binary storage with version tracking, branch management, and volatile binary support.
/// </summary>
public class BinaryRegistry
{
    private readonly ILogger<BinaryRegistry> _logger;
    private readonly string _registryPath;
    private readonly string _branchesPath;
    private readonly string _volatilePath;
    private readonly object _lock = new();
    private Dictionary<string, BinaryInfo>? _cache;

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public BinaryRegistry(ILogger<BinaryRegistry> logger, string? registryPath = null)
    {
        _logger = logger;
        _registryPath = registryPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenLMStudio", "engines", "registry.json");
        _branchesPath = Path.Combine(Path.GetDirectoryName(_registryPath)!, "branches");
        _volatilePath = Path.Combine(Path.GetDirectoryName(_registryPath)!, "volatile");
    }

    /// <summary>
    /// Registers a downloaded binary.
    /// </summary>
    public BinaryInfo RegisterDownloadedBinary(BinaryInfo info)
    {
        lock (_lock)
        {
            (_cache ??= new()).AddOrUpdate(info.Id, info);
            SaveRegistry();
        }
        return info;
    }

    /// <summary>
    /// Registers a locally compiled binary.
    /// </summary>
    public BinaryInfo RegisterLocalBinary(string branchName, string binaryPath, string? gitCommit = null)
    {
        var id = $"llama-server-branch-{branchName}";
        var info = new BinaryInfo(
            Id: id,
            Name: "llama-server",
            Backend: BackendType.Cpu,
            Platform: GetPlatform(),
            Architecture: GetArchitecture(),
            Version: gitCommit?.Substring(0, 7) ?? "local",
            Checksum: null,
            DownloadUrl: null,
            DownloadDate: DateTime.UtcNow,
            IsBuiltLocally: true,
            GitBranch: branchName,
            GitCommit: gitCommit,
            BuildDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
            BuildFlags: null,
            BinaryPath: binaryPath,
            ManifestPath: null
        );

        // Create manifest
        var manifestDir = Path.Combine(_branchesPath, branchName);
        Directory.CreateDirectory(manifestDir);
        var manifestPath = Path.Combine(manifestDir, "build-info.json");
        var manifest = new
        {
            branch = branchName,
            commit = gitCommit,
            buildDate = info.BuildDate,
            buildFlags = info.BuildFlags,
            binaryPath = binaryPath
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, _jsonOptions));

        info = info with { ManifestPath = manifestPath };

        lock (_lock)
        {
            (_cache ??= new()).AddOrUpdate(info.Id, info);
            SaveRegistry();
        }

        _logger.LogInformation("Registered local binary: {Branch} at {Path}", branchName, binaryPath);
        return info;
    }

    /// <summary>
    /// Registers a volatile (user-compiled) binary.
    /// </summary>
    public BinaryInfo RegisterVolatileBinary(string name, string binaryPath, string buildFlags)
    {
        var id = $"llama-server-volatile-{name}";
        var info = new BinaryInfo(
            Id: id,
            Name: name,
            Backend: BackendType.Cpu,
            Platform: GetPlatform(),
            Architecture: GetArchitecture(),
            Version: "volatile",
            Checksum: null,
            DownloadUrl: null,
            DownloadDate: DateTime.UtcNow,
            IsBuiltLocally: true,
            GitBranch: null,
            GitCommit: null,
            BuildDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
            BuildFlags: buildFlags,
            BinaryPath: binaryPath,
            ManifestPath: null
        );

        var manifestDir = Path.Combine(_volatilePath, name);
        Directory.CreateDirectory(manifestDir);
        var manifestPath = Path.Combine(manifestDir, "build-info.json");
        var manifest = new { name, buildFlags, buildDate = info.BuildDate, binaryPath };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, _jsonOptions));

        info = info with { ManifestPath = manifestPath };

        lock (_lock)
        {
            (_cache ??= new()).AddOrUpdate(info.Id, info);
            SaveRegistry();
        }

        return info;
    }

    /// <summary>
    /// Gets all registered binaries.
    /// </summary>
    public IReadOnlyList<BinaryInfo> GetBinaries()
    {
        lock (_lock)
        {
            if (_cache == null)
            {
                LoadRegistry();
            }
            return (_cache ??= new()).Values.ToList();
        }
    }

    /// <summary>
    /// Gets a binary by ID.
    /// </summary>
    public BinaryInfo? GetBinary(string id)
    {
        lock (_lock)
        {
            if (_cache == null)
                LoadRegistry();
            return _cache?.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Removes a binary from the registry (does not delete the file).
    /// </summary>
    public void RemoveBinary(string id)
    {
        lock (_lock)
        {
            _cache?.Remove(id);
            SaveRegistry();
        }
    }

    /// <summary>
    /// Finds the best binary for a given backend.
    /// </summary>
    public BinaryInfo? GetBestForBackend(BackendType backend)
    {
        var binaries = GetBinaries();
        return binaries.FirstOrDefault(b => b.Backend == backend)
            ?? binaries.FirstOrDefault(b => b.Backend == BackendType.Cpu);
    }

    private void LoadRegistry()
    {
        try
        {
            if (File.Exists(_registryPath))
            {
                var json = File.ReadAllText(_registryPath);
                var binaries = JsonSerializer.Deserialize<List<BinaryInfo>>(json);
                if (binaries != null)
                {
                    _cache = binaries.ToDictionary(b => b.Id, b => b);
                    _logger.LogDebug("Loaded {Count} binaries from registry", _cache.Count);
                }
            }
            else
            {
                _cache = new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load binary registry, starting fresh");
            _cache = new();
        }
    }

    private void SaveRegistry()
    {
        try
        {
            var dir = Path.GetDirectoryName(_registryPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = _registryPath + ".tmp";
            var json = JsonSerializer.Serialize(_cache!.Values.ToList(), _jsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _registryPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save binary registry");
        }
    }

    private static string GetPlatform()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)) return "windows";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)) return "linux";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)) return "macos";
        return "linux";
    }

    private static string GetArchitecture()
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        return arch switch
        {
            "x64" => "x64",
            "arm64" => "arm64",
            _ => arch
        };
    }
}

internal static class DictionaryExtensions
{
    public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        where TKey : notnull
    {
        dict[key] = value;
    }
}