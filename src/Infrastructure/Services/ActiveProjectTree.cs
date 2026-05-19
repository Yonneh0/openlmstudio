using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IActiveProjectWatcher that provides real-time project tree watching and file preview.
/// </summary>
public class ActiveProjectTree : IActiveProjectWatcher, IDisposable
{
    private readonly ILogger<ActiveProjectTree>? _logger;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    /// <inheritdoc />
    public string RootPath { get; private set; } = string.Empty;

    /// <inheritdoc />
    public bool IsWatching => _watcher?.EnableRaisingEvents == true;

    /// <inheritdoc />
    public event EventHandler<FileSystemChangeEventArgs>? FileSystemChanged;

    /// <summary>
    /// Creates a new ActiveProjectTree instance.
    /// </summary>
    public ActiveProjectTree(ILogger<ActiveProjectTree>? logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (string.IsNullOrEmpty(RootPath) || IsWatching) return;

        try
        {
            _watcher = new FileSystemWatcher(RootPath);
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start ActiveProjectTree watching");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_watcher == null) return;

        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileCreated;
            _watcher.Deleted -= OnFileDeleted;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop FileSystemWatcher");
        }
        finally
        {
            _watcher = null!;
        }
    }

    /// <inheritdoc />
    public async Task RefreshTreeAsync()
    {
        if (string.IsNullOrEmpty(RootPath)) return;

        try
        {
            var tree = GetProjectTreeInternal();
            _logger?.LogDebug("Refreshed project tree: {Count} root nodes", tree.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh project tree");
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectNode>> GetProjectTreeAsync(string? rootPath = null)
    {
        var tree = GetProjectTreeInternal(rootPath);
        return Task.FromResult<IReadOnlyList<ProjectNode>>(new List<ProjectNode>(tree));
    }

    /// <inheritdoc />
    public async Task<FilePreviewResult?> GetFilePreviewAsync(string filePath, int maxLines = 100)
    {
        if (IsBinaryFile(filePath)) return null;

        try
        {
            var allLines = await File.ReadAllLinesAsync(filePath);
            var isTruncated = allLines.Length > maxLines;
            var linesToReturn = isTruncated ? allLines.Take(maxLines).ToList() : allLines.ToList();

            return new FilePreviewResult(
                filePath,
                linesToReturn.AsReadOnly(),
                isTruncated,
                allLines.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read file preview: {FilePath}", filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            // Read first 8 bytes and check for binary indicators
            var buffer = new byte[8];
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length) return false;

            // Check for common null byte patterns in binary files
            if ((buffer[0] == 0x00 && buffer[1] != 0x00) ||   // UTF-16 LE BOM without data
                (buffer[0] != 0x00 && buffer[1] == 0x00))      // UTF-16 BE BOM without data
            {
                return true;
            }

            // Check for null bytes in first 1KB — if found, likely binary
            const int sampleSize = 1024;
            var readBytes = Math.Min((int)Math.Max(8, stream.Length), sampleSize);
            if (readBytes > 0)
            {
                var tempBuffer = new byte[readBytes];
                if (stream.Read(tempBuffer, 0, readBytes) == readBytes)
                {
                    for (var i = 0; i < readBytes; i++)
                        if (tempBuffer[i] == 0x00 && i >= 4) // Skip first few bytes which might be BOM
                            return true;
                }
            }

            return false;
        }
        catch
        {
            // If we can't read the file, assume it's text (file might not exist yet)
            return false;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string?>> GetGitStatusAsync() => Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());

    private ProjectNode[] GetProjectTreeInternal(string? rootPath = null)
    {
        var basePath = rootPath ?? RootPath;
        if (string.IsNullOrEmpty(basePath)) return Array.Empty<ProjectNode>();

        try
        {
            var nodes = new List<ProjectNode>();
            AddDirectoryNodes(basePath, nodes);
            return nodes.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to build project tree for: {Path}", basePath);
            return Array.Empty<ProjectNode>();
        }
    }

    private void AddDirectoryNodes(string path, List<ProjectNode> nodes)
    {
        try
        {
            // Get directories first — they need to be added before files for proper hierarchy
            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs.OrderBy(d => Path.GetFileName(d)))
            {
                var node = new ProjectNode(
                    dir,
                    IsDirectory: true,
                    SizeBytes: 0,
                    LastModified: Directory.GetLastWriteTime(dir));

                nodes.Add(node);

                // Recurse into subdirectory — we need a nested list for children
                var childNodes = new List<ProjectNode>();
                AddFileNodesForDirectory(dir, childNodes);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to add directory nodes for: {Path}", path);
        }

        // Get files — they can only be added after their parent directory exists in the tree
        var files = Directory.GetFiles(path).OrderBy(f => Path.GetFileName(f));
        foreach (var file in files)
        {
            try
            {
                var node = new ProjectNode(
                    file,
                    IsDirectory: false,
                    SizeBytes: new FileInfo(file).Length,
                    LastModified: File.GetLastWriteTime(file));

                nodes.Add(node);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to add file node for: {Path}", file);
            }
        }
    }

    private void AddFileNodesForDirectory(string path, List<ProjectNode> childNodes)
    {
        try
        {
            var dirs = Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d));
            foreach (var dir in dirs)
            {
                var node = new ProjectNode(
                    dir,
                    IsDirectory: true,
                    SizeBytes: 0,
                    LastModified: Directory.GetLastWriteTime(dir));

                childNodes.Add(node);

                // Recurse into subdirectory
                var grandChildNodes = new List<ProjectNode>();
                AddFileNodesForDirectory(dir, grandChildNodes);
            }
        }
        catch { /* Ignore errors in recursive directory traversal */ }
    }

    private string GetHashCodeForPath(string path) => Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(path)).Substring(0, 16);

    // ---- FileSystemWatcher Event Handlers ----

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;

        var notification = new FileSystemChangeNotification
        {
            ChangeType = FileWatchEventType.Modified,
            Path = e.FullPath,
            ChangedAt = DateTime.UtcNow
        };

        try
        {
            FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(notification));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to invoke FileSystemChanged event");
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var notification = new FileSystemChangeNotification
        {
            ChangeType = FileWatchEventType.Added,
            Path = e.FullPath,
            ChangedAt = DateTime.UtcNow
        };

        try
        {
            FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(notification));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to invoke FileSystemChanged event");
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var notification = new FileSystemChangeNotification
        {
            ChangeType = FileWatchEventType.Deleted,
            Path = e.FullPath,
            ChangedAt = DateTime.UtcNow
        };

        try
        {
            FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(notification));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to invoke FileSystemChanged event");
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var notification = new FileSystemChangeNotification
        {
            ChangeType = FileWatchEventType.Renamed,
            Path = e.FullPath,  // New path after rename
            ChangedAt = DateTime.UtcNow
        };

        try
        {
            FileSystemChanged?.Invoke(this, new FileSystemChangeEventArgs(notification));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to invoke FileSystemChanged event");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Cancel async cleanup to avoid blocking Dispose — use Task.Run with timeout to prevent deadlocks
            var disposeTask = StopAsync();
            try
            {
                if (!disposeTask.IsCompleted && disposeTask.Wait(TimeSpan.FromSeconds(2)))
                    disposeTask.GetAwaiter().GetResult();
            }
            catch
            {
                // Timeout or cancellation — acceptable during disposal
            }

            try
            {
                _watcher?.Dispose();
            }
            catch { /* Ignore disposal errors */ }

            _logger?.LogDebug("ActiveProjectTree disposed");
        }
    }
}