namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages context snapshots for tasks, providing save/load of compressed history,
/// tool results cache, and project state.
/// </summary>
public class ContextSnapshotManager : IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly string _snapshotDirectory;
    private readonly ConcurrentDictionary<Guid, TaskContextSnapshot> _inMemorySnapshots;
    private readonly ILogger<ContextSnapshotManager> _logger;
    private readonly object _lock = new();

    public ContextSnapshotManager(
        IFileSystem? fileSystem = null,
        string? snapshotDirectory = null,
        ILogger<ContextSnapshotManager>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _snapshotDirectory = snapshotDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "snapshots");
        _inMemorySnapshots = new ConcurrentDictionary<Guid, TaskContextSnapshot>();
        _logger = logger ?? NullLogger<ContextSnapshotManager>.Instance;
    }

    public void Dispose()
    {
        foreach (var snapshot in _inMemorySnapshots)
        {
            snapshot.Value?.Dispose();
        }
        _inMemorySnapshots.Clear();
    }

    /// <summary>
    /// Creates a context snapshot for a task.
    /// </summary>
    public async Task<TaskContextSnapshot> CreateSnapshotAsync(Guid taskId, CancellationToken ct = default)
    {
        var snapshot = new TaskContextSnapshot
        {
            TaskId = taskId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await SaveSnapshotAsync(taskId, snapshot, ct);
        return snapshot;
    }

    /// <summary>
    /// Gets a snapshot for a task, loading from file if not in memory.
    /// </summary>
    public async Task<TaskContextSnapshot?> GetSnapshotAsync(Guid taskId, CancellationToken ct = default)
    {
        if (_inMemorySnapshots.TryGetValue(taskId, out var snapshot))
            return snapshot;

        var snapshotPath = GetSnapshotFilePath(taskId);
        if (!_fileSystem.File.Exists(snapshotPath))
            return null;

        var json = await _fileSystem.File.ReadAllTextAsync(snapshotPath, ct);
        var loadedSnapshot = JsonSerializer.Deserialize<TaskContextSnapshot>(json);

        if (loadedSnapshot != null)
        {
            _inMemorySnapshots[taskId] = loadedSnapshot;
        }

        return loadedSnapshot;
    }

    /// <summary>
    /// Saves a snapshot to file and updates in-memory cache.
    /// </summary>
    public async Task SaveSnapshotAsync(Guid taskId, TaskContextSnapshot snapshot, CancellationToken ct = default)
    {
        try
        {
            snapshot.UpdatedAt = DateTime.UtcNow;
            var snapshotPath = GetSnapshotFilePath(taskId);
            var dir = _fileSystem.Path.GetDirectoryName(snapshotPath);
            if (!string.IsNullOrEmpty(dir))
                _fileSystem.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.File.WriteAllTextAsync(snapshotPath, json, ct);

            lock (_lock)
            {
                _inMemorySnapshots[taskId] = snapshot;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving snapshot for task {TaskId}", taskId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a snapshot for a task.
    /// </summary>
    public async Task DeleteSnapshotAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var snapshotPath = GetSnapshotFilePath(taskId);
            if (_fileSystem.File.Exists(snapshotPath))
                _fileSystem.File.Delete(snapshotPath);

            _inMemorySnapshots.TryRemove(taskId, out _);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting snapshot for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Gets all snapshots.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, TaskContextSnapshot>> GetAllSnapshotsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new Dictionary<Guid, TaskContextSnapshot>();
            foreach (var kvp in _inMemorySnapshots)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all snapshots");
            return new Dictionary<Guid, TaskContextSnapshot>().AsReadOnly();
        }
    }

    private string GetSnapshotFilePath(Guid taskId)
    {
        return Path.Combine(_snapshotDirectory, $"{taskId:N}.json");
    }
}