// Brought to you by Carls' Jr.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Monitors real-time changes to the active project tree using FileSystemWatcher.
/// </summary>
public class ActiveProjectWatcher : IActiveProjectWatcher
{
    private readonly ILogger<ActiveProjectWatcher>? _logger;
    private readonly string _watchPath;
    private readonly IProjectExplorer? _projectExplorer;
    private FileSystemWatcher? _watcher;
    private Application.Interfaces.ProjectTreeNode? _currentTree;
    private string? _currentDirectory;
    private bool _disposed;

    public ActiveProjectWatcher(ILogger<ActiveProjectWatcher>? logger = null, string? watchPath = null, IProjectExplorer? projectExplorer = null)
    {
        _logger = logger;
        _watchPath = watchPath ?? Directory.GetCurrentDirectory();
        _projectExplorer = projectExplorer;
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
                           NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;

        _watcher.EnableRaisingEvents = true;
        _logger?.LogInformation("Started watching directory: {Directory}", directoryPath);
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
        var change = new Application.Interfaces.ProjectTreeChange(e.FullPath, Application.Interfaces.ProjectTreeChangeType.Renamed, e.OldFullPath);
        TreeChanged?.Invoke(this, change);
    }

    private async Task<Application.Interfaces.ProjectTreeNode> BuildTreeAsync(string directoryPath, CancellationToken ct)
    {
        var entries = new List<Application.Interfaces.ProjectTreeNode>();

        try
        {
            var dirs = Directory.EnumerateDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(d => Path.GetFileName(d));
            var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(f => Path.GetFileName(f));

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