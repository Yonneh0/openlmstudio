using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Real-time filesystem watcher for project tree updates.
/// Monitors a directory for file additions, modifications, and deletions.
/// </summary>
public class ActiveProjectWatcher : IActiveProjectWatcher
{
    private readonly ILogger<ActiveProjectWatcher>? _logger;
    private readonly IProjectExplorer _projectExplorer;
    private readonly string _rootPath;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isWatching;

    public ActiveProjectWatcher(ILogger<ActiveProjectWatcher>? logger, string rootPath, IProjectExplorer projectExplorer)
    {
        _logger = logger;
        _rootPath = rootPath;
        _projectExplorer = projectExplorer;
    }

    public string RootPath => _rootPath;
    public bool IsWatching => _isWatching;

    public async Task StartAsync()
    {
        if (_disposed || _isWatching) return;

        _watcher = new FileSystemWatcher(_rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;
        _isWatching = true;
        _logger?.LogInformation("ActiveProjectWatcher started for directory: {Directory}", _rootPath);
    }

    public async Task StopAsync()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _watcher = null;
            _isWatching = false;
        }
        _logger?.LogInformation("ActiveProjectWatcher stopped for {Directory}", _rootPath);
        await Task.CompletedTask;
    }

    public async Task RefreshTreeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ProjectNode>> GetProjectTreeAsync(string? rootPath = null)
    {
        return await _projectExplorer.GetProjectTreeAsync(rootPath ?? _rootPath);
    }

    public async Task<FilePreviewResult?> GetFilePreviewAsync(string filePath, int maxLines = 100)
    {
        return await _projectExplorer.GetFilePreviewAsync(filePath, maxLines);
    }

    public bool IsBinaryFile(string filePath)
    {
        return _projectExplorer.IsBinaryFile(filePath);
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetGitStatusAsync()
    {
        return await _projectExplorer.GetGitStatusAsync(_rootPath);
    }

    public event EventHandler<FileSystemChangeEventArgs>? FileSystemChanged;

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var eventType = e.ChangeType switch
        {
            WatcherChangeTypes.Created => FileWatchEventType.Added,
            WatcherChangeTypes.Deleted => FileWatchEventType.Deleted,
            WatcherChangeTypes.Changed => FileWatchEventType.Modified,
            _ => FileWatchEventType.Modified
        };

        var notification = new FileSystemChangeNotification(eventType, e.FullPath, DateTime.UtcNow);

        _logger?.LogDebug("File change detected: {EventType} - {FullPath}", eventType, e.FullPath);
        FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(notification));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var eventType = string.IsNullOrEmpty(e.OldName)
            ? FileWatchEventType.Added
            : FileWatchEventType.Modified;

        var notification = new FileSystemChangeNotification(eventType, e.FullPath, DateTime.UtcNow);

        _logger?.LogDebug("File renamed: {OldPath} -> {NewPath}", e.FullPath, e.Name);
        FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(notification));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
