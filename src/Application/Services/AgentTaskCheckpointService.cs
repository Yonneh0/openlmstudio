namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages task checkpoints with file-based storage.
/// </summary>
public class AgentTaskCheckpointService : IAgentTaskCheckpointService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _checkpointDirectory;
    private readonly ConcurrentDictionary<Guid, List<TaskCheckpoint>> _inMemoryCheckpoints;
    private readonly ILogger<AgentTaskCheckpointService> _logger;
    private readonly object _lock = new();

    public AgentTaskCheckpointService(
        IFileSystem? fileSystem = null,
        string? checkpointDirectory = null,
        ILogger<AgentTaskCheckpointService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _checkpointDirectory = checkpointDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "checkpoints");
        _inMemoryCheckpoints = new ConcurrentDictionary<Guid, List<TaskCheckpoint>>();
        _logger = logger ?? NullLogger<AgentTaskCheckpointService>.Instance;
    }

    public void Dispose()
    {
        _inMemoryCheckpoints.Clear();
    }

    public async Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken ct = default)
    {
        try
        {
            var taskDir = GetTaskDirectory(taskId);
            _fileSystem.Directory.CreateDirectory(taskDir);

            var checkpoints = _inMemoryCheckpoints.GetOrAdd(taskId, _ => new List<TaskCheckpoint>());
            int version;
            lock (_lock)
            {
                version = checkpoints.Count + 1;
            }
            checkpoint.Version = version;
            checkpoints.Add(checkpoint);

            var checkpointPath = Path.Combine(taskDir, $"checkpoint_{checkpoint.Version}.json");
            var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.File.WriteAllTextAsync(checkpointPath, json, ct);

            _logger?.LogDebug("Checkpoint saved for task {TaskId} version {Version}", taskId, checkpoint.Version);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving checkpoint for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task<TaskCheckpoint?> GetLatestCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var checkpoints = await GetCheckpointsAsync(taskId, ct);
            return checkpoints.OrderByDescending(c => c.Version).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting latest checkpoint for task {TaskId}", taskId);
            return null;
        }
    }

    public async Task<IReadOnlyList<TaskCheckpoint>> GetCheckpointsAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var taskDir = GetTaskDirectory(taskId);
            if (!_fileSystem.Directory.Exists(taskDir))
                return new List<TaskCheckpoint>().AsReadOnly();

            var checkpoints = new List<TaskCheckpoint>();

            var files = _fileSystem.Directory.GetFiles(taskDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await _fileSystem.File.ReadAllTextAsync(file, ct);
                    var checkpoint = JsonSerializer.Deserialize<TaskCheckpoint>(json);
                    if (checkpoint != null)
                        checkpoints.Add(checkpoint);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error reading checkpoint file {File}", file);
                }
            }

            return checkpoints.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting checkpoints for task {TaskId}", taskId);
            return new List<TaskCheckpoint>().AsReadOnly();
        }
    }

    public async Task<TaskCheckpoint?> GetLatestToolCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoints = await GetCheckpointsAsync(taskId, ct);
        return checkpoints
            .Where(c => c.Type == CheckpointType.Tool)
            .OrderByDescending(c => c.Version)
            .FirstOrDefault();
    }

    public async Task<TaskCheckpoint?> GetLatestCompletionCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoints = await GetCheckpointsAsync(taskId, ct);
        return checkpoints
            .Where(c => c.Type == CheckpointType.Completion)
            .OrderByDescending(c => c.Version)
            .FirstOrDefault();
    }

    public async Task DeleteCheckpointsAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var taskDir = GetTaskDirectory(taskId);
            if (_fileSystem.Directory.Exists(taskDir))
            {
                _fileSystem.Directory.Delete(taskDir, true);
            }

            _inMemoryCheckpoints.TryRemove(taskId, out _);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting checkpoints for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task SaveToolCheckpointAsync(Guid taskId, string toolName, Dictionary<string, object> parameters, string result, CancellationToken ct = default)
    {
        var checkpoint = TaskCheckpoint.CreateToolCheckpoint(taskId, toolName, parameters, result);
        await SaveCheckpointAsync(taskId, checkpoint, ct);
    }

    public async Task SaveTaskCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoint = TaskCheckpoint.CreateTaskCheckpoint(taskId);
        await SaveCheckpointAsync(taskId, checkpoint, ct);
    }

    public async Task SaveCompletionCheckpointAsync(Guid taskId, CancellationToken ct = default)
    {
        var checkpoint = TaskCheckpoint.CreateCompletionCheckpoint(taskId);
        await SaveCheckpointAsync(taskId, checkpoint, ct);
    }

    private string GetTaskDirectory(Guid taskId)
    {
        return Path.Combine(_checkpointDirectory, taskId.ToString("N"));
    }
}