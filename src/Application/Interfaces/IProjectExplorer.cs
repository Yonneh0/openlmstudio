using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenLMStudio.Application.Interfaces;

// ProjectNode is already defined in IFileOperations.cs as a record type.
// Use that type for the project tree structure — it has Path, Name, IsDirectory, SizeBytes, LastModified, and Children properties.

/// <summary>
/// Represents a real-time filesystem change notification.
/// </summary>
public class FileSystemChangeNotification
{
    /// <summary>
    /// The type of file system change (Added, Modified, Deleted).
    /// </summary>
    public FileWatchEventType ChangeType { get; set; }

    /// <summary>
    /// Path to the affected file or directory.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of when the change occurred.
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event for real-time filesystem changes.
/// </summary>
public class FileSystemChangeEventArgs : EventArgs
{
    public FileSystemChangeNotification Notification { get; }

    public FileSystemChangeEventArgs(FileSystemChangeNotification notification) => Notification = notification;
}

/// <summary>
/// File system watch event type enumeration.
/// </summary>
public enum FileWatchEventType
{
    /// <summary>A new file or directory was created.</summary>
    Added,
    /// <summary>An existing file or directory was modified.</summary>
    Modified,
    /// <summary>A file or directory was deleted.</summary>
    Deleted,
    /// <summary>A rename operation occurred (for files).</summary>
    Renamed
}

/// <summary>
/// Interface for real-time project tree watching and notification.
/// </summary>
public interface IActiveProjectWatcher : IDisposable
{
    /// <summary>
    /// Gets the root path of the monitored project directory.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// Whether the watcher is currently active and monitoring for changes.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Starts watching the project directory for real-time filesystem changes.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops watching the project directory for changes.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Refreshes the project tree from disk without stopping the watcher.
    /// </summary>
    Task RefreshTreeAsync();

    /// <summary>
    /// Gets the current project tree rooted at the specified path.
    /// </summary>
    Task<IReadOnlyList<ProjectNode>> GetProjectTreeAsync(string? rootPath = null);

    /// <summary>
    /// Gets file preview content for a text file (first N lines).
    /// </summary>
    Task<FilePreviewResult?> GetFilePreviewAsync(string filePath, int maxLines = 100);

    /// <summary>
    /// Determines if a file is binary and cannot be previewed.
    /// </summary>
    bool IsBinaryFile(string filePath);

    /// <summary>
    /// Gets git status for all files in the project directory (if within a git repository).
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetGitStatusAsync();

    /// <summary>
    /// Event fired when filesystem changes are detected.
    /// </summary>
    event EventHandler<FileSystemChangeEventArgs>? FileSystemChanged;
}

/// <summary>
/// Result of a file preview operation.
/// </summary>
public class FilePreviewResult
{
    public string FilePath { get; }
    public IReadOnlyList<string> Lines { get; }
    public bool IsTruncated { get; }
    public int TotalLines { get; }

    public FilePreviewResult(string filePath, IReadOnlyList<string> lines, bool isTruncated, int totalLines)
    {
        FilePath = filePath;
        Lines = lines;
        IsTruncated = isTruncated;
        TotalLines = totalLines;
    }
}