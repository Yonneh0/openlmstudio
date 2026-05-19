using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Real-time project filesystem watcher that monitors a directory for file changes.
/// Notifies subscribers via events when files are added, modified, or deleted.
/// </summary>
public interface IActiveProjectWatcher : IDisposable
{
    /// <summary>
    /// Raised when a file in the watched project changes.
    /// </summary>
    event EventHandler<ProjectFileChangedEventArgs>? ProjectFileChanged;
}

/// <summary>
/// Event arguments for project file changes.
/// </summary>
public class ProjectFileChangedEventArgs : EventArgs
{
    public string FilePath { get; }
    public FileEventType EventType { get; }

    public ProjectFileChangedEventArgs(string filePath, FileEventType eventType)
    {
        FilePath = filePath;
        EventType = eventType;
    }
}

/// <summary>
/// Types of file system events.
/// </summary>
public enum FileEventType
{
    Added,
    Modified,
    Deleted,
    Renamed
}

/// <summary>
/// Provides real-time filesystem monitoring for a project directory.
/// Emits events for additions, modifications, and deletions.
/// </summary>
public class ActiveProjectWatcher : IActiveProjectWatcher, IDisposable
{
    private readonly ILogger<ActiveProjectWatcher>? _logger;
    private readonly FileSystemWatcher? _watcher;
    private bool _disposed;

    public event EventHandler<ProjectFileChangedEventArgs>? ProjectFileChanged;

    public ActiveProjectWatcher(ILogger<ActiveProjectWatcher>? logger, string watchPath)
    {
        _logger = logger;
        _watcher = new FileSystemWatcher(watchPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;
        _logger?.LogInformation("Started watching directory: {Path}", watchPath);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var eventType = e.ChangeType switch
        {
            WatcherChangeTypes.Created => FileEventType.Added,
            WatcherChangeTypes.Changed => FileEventType.Modified,
            WatcherChangeTypes.Deleted => FileEventType.Deleted,
            _ => FileEventType.Modified
        };

        ProjectFileChanged?.Invoke(this, new ProjectFileChangedEventArgs(e.FullPath, eventType));
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        ProjectFileChanged?.Invoke(this, new ProjectFileChangedEventArgs(e.FullPath, FileEventType.Renamed));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _watcher?.Dispose();
            _disposed = true;
        }
    }
}