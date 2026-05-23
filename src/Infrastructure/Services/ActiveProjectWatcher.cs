// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Monitors real-time changes to the active project tree using FileSystemWatcher.
/// Supports ignore patterns (node_modules, .git, etc.) and event debouncing for large projects.
/// </summary>
public class ActiveProjectWatcher : IActiveProjectWatcher
{
    private readonly ILogger<ActiveProjectWatcher>? _logger;
    private readonly string _watchPath;
    private readonly IProjectExplorer? _projectExplorer;
    private readonly HashSet<string> _ignorePatterns;
    private readonly HashSet<string> _ignoreExtensions;
    private FileSystemWatcher? _watcher;
    private Application.Interfaces.ProjectTreeNode? _currentTree;
    private string? _currentDirectory;
    private bool _disposed;

    // Debouncing
    private readonly Dictionary<string, long> _lastChangeTimes = new(StringComparer.OrdinalIgnoreCase);
    private const long DebounceIntervalMs = 300;
    private long _debounceTimer;

    public ActiveProjectWatcher(ILogger<ActiveProjectWatcher>? logger = null, string? watchPath = null, IProjectExplorer? projectExplorer = null)
    {
        _logger = logger;
        _watchPath = watchPath ?? Directory.GetCurrentDirectory();
        _projectExplorer = projectExplorer;

        // Default ignore patterns for common project types
        _ignorePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules",
            ".git",
            ".vs",
            ".vscode",
            "bin",
            "obj",
            ".idea",
            ".DS_Store",
            "Thumbs.db",
            "__pycache__",
            ".pytest_cache",
            ".mypy_cache",
            "venv",
            ".venv",
            "env",
            "packages",
            "vendor",
            ".nuget",
            "AppData",
            "temp",
            ".cache",
        };

        // Default ignore file extensions
        _ignoreExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".log",
            ".pdb",
            ".dll",
            ".exe",
            ".zip",
            ".tmp",
            ".cache",
            ".orig",
            ".bak",
            ".suo",
            ".user",
            ".lock",
            ".lockb",
            ".lock.json",
        };
    }

    public Application.Interfaces.ProjectTreeNode? CurrentTree => _currentTree;

    public event System.EventHandler<Application.Interfaces.ProjectTreeChange>? TreeChanged;

    public async Task StartAsync(string directoryPath, CancellationToken ct = default)
    {
        StopAsync(ct).GetAwaiter().GetResult();

        _currentDirectory = directoryPath;
        _currentTree = await BuildTreeAsync(directoryPath, ct).ConfigureAwait(false);

        _watcher = new FileSystemWatcher(directoryPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            // Buffer size: increase for large projects with many files
            InternalBufferSize = 64 * 1024, // 64KB
            EnableRaisingEvents = false,
        };

        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        _watcher.EnableRaisingEvents = true;
        _logger?.LogInformation("Started watching directory: {Directory} (pattern count: {Patterns}, buffer: {BufferSize}KB)",
            directoryPath, _ignorePatterns.Count, _watcher.InternalBufferSize / 1024);
    }

    private bool ShouldIgnorePath(string fullPath)
    {
        var dirName = Path.GetDirectoryName(fullPath);
        if (dirName != null)
        {
            foreach (var pattern in _ignorePatterns)
            {
                if (dirName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        var ext = Path.GetExtension(fullPath);
        if (_ignoreExtensions.Contains(ext))
            return true;

        var fileName = Path.GetFileName(fullPath);
        if (fileName.StartsWith('.') && fileName.Length <= 2)
            return true;

        return false;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        _logger?.LogInformation("Stopped watching directory: {Directory}", _currentDirectory);
    }

    public async Task<Application.Interfaces.ProjectTreeNode> RefreshAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_currentDirectory))
            throw new InvalidOperationException("Watcher not started. Call StartAsync first.");

        _currentTree = await BuildTreeAsync(_currentDirectory, ct).ConfigureAwait(false);
        return _currentTree;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnorePath(e.FullPath))
            return;

        // Debounce: only process if this file hasn't changed recently
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var lastChange = _lastChangeTimes.GetValueOrDefault(e.FullPath, 0);
        if (now - lastChange < DebounceIntervalMs)
            return;

        _lastChangeTimes[e.FullPath] = now;

        // Clean up old entries periodically (every 100 events)
        if (_debounceTimer++ % 100 == 0)
        {
            var cutoff = now - 60_000; // 1 minute
            foreach (var key in _lastChangeTimes.Keys.ToList())
            {
                if (_lastChangeTimes[key] < cutoff)
                    _lastChangeTimes.Remove(key);
            }
        }

        var changeType = e.ChangeType switch
        {
            WatcherChangeTypes.Created => Application.Interfaces.ProjectTreeChangeType.Added,
            WatcherChangeTypes.Deleted => Application.Interfaces.ProjectTreeChangeType.Deleted,
            _ => Application.Interfaces.ProjectTreeChangeType.Modified
        };

        var change = new Application.Interfaces.ProjectTreeChange(e.FullPath, changeType);
        TreeChanged?.Invoke(this, change);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldIgnorePath(e.FullPath))
            return;

        var change = new Application.Interfaces.ProjectTreeChange(e.FullPath, Application.Interfaces.ProjectTreeChangeType.Renamed, e.OldFullPath);
        TreeChanged?.Invoke(this, change);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger?.LogWarning(e.GetException(), "FileSystemWatcher error in: {Directory}", _currentDirectory);
    }

    public async Task<Application.Interfaces.ProjectTreeNode> BuildTreeAsync(string directoryPath, CancellationToken ct)
    {
        var entries = new List<Application.Interfaces.ProjectTreeNode>();

        try
        {
            var dirs = Directory.EnumerateDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(d => !_ignorePatterns.Contains(Path.GetFileName(d)))
                .OrderBy(d => Path.GetFileName(d));
            var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => !_ignoreExtensions.Contains(Path.GetExtension(f)));

            foreach (var dir in dirs)
            {
                if (ct.IsCancellationRequested) break;
                var dirInfo = new DirectoryInfo(dir);
                var childTree = await BuildTreeAsync(dir, ct).ConfigureAwait(false);
                entries.Add(new Application.Interfaces.ProjectTreeNode(
                    dir,
                    dirInfo.Name,
                    true,
                    new List<Application.Interfaces.ProjectTreeNode> { childTree },
                    null,
                    dirInfo.LastWriteTime));
            }

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;
                var fileInfo = new FileInfo(file);
                entries.Add(new Application.Interfaces.ProjectTreeNode(
                    file,
                    fileInfo.Name,
                    false,
                    Array.Empty<Application.Interfaces.ProjectTreeNode>(),
                    fileInfo.Length,
                    fileInfo.LastWriteTime));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to build project tree for: {Directory}", directoryPath);
            entries.Add(new Application.Interfaces.ProjectTreeNode(directoryPath, "Error loading", false, Array.Empty<Application.Interfaces.ProjectTreeNode>()));
        }

        return new Application.Interfaces.ProjectTreeNode(directoryPath, Path.GetFileName(directoryPath), true, entries);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }
}
