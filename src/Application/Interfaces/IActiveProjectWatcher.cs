using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Event type for filesystem changes detected by the active project watcher.
/// </summary>
public enum FileWatchEventType
{
    Added,
    Deleted,
    Modified,
    Renamed
}

/// <summary>
/// Notification containing details of a filesystem change.
/// </summary>
public record FileSystemChangeNotification(
    FileWatchEventType ChangeType,
    string Path,
    System.DateTimeOffset ChangedAt);

/// <summary>
/// Event arguments for filesystem change notifications.
/// </summary>
public class FileSystemChangeEventArgs : EventArgs
{
    public FileSystemChangeNotification Notification { get; }

    public FileSystemChangeEventArgs(FileSystemChangeNotification notification)
    {
        Notification = notification;
    }
}

/// <summary>
/// Service for monitoring a directory and emitting filesystem change events.
/// Used by the agent harness to keep the project tree in sync with real-time changes.
/// </summary>
public interface IActiveProjectWatcher
{
    /// <summary>
    /// Root path being watched.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Whether the watcher is currently active.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Starts monitoring the root path.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Refreshes the project tree from disk.
    /// </summary>
    Task RefreshTreeAsync();

    /// <summary>
    /// Gets the current project tree.
    /// </summary>
    Task<IReadOnlyList<ProjectNode>> GetProjectTreeAsync(string? rootPath = null);

    /// <summary>
    /// Gets a file preview.
    /// </summary>
    Task<FilePreviewResult?> GetFilePreviewAsync(string filePath, int maxLines = 100);

    /// <summary>
    /// Checks if a file is binary.
    /// </summary>
    bool IsBinaryFile(string filePath);

    /// <summary>
    /// Gets git status for files in the watched directory.
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetGitStatusAsync();

    /// <summary>
    /// Event raised when filesystem changes are detected.
    /// </summary>
    event EventHandler<FileSystemChangeEventArgs> FileSystemChanged;
}