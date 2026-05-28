namespace OpenLMStudio.Application.Services;

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages agent task state persistence with file-based storage.
/// </summary>
public class AgentTaskStateService : IAgentTaskStateService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _stateDirectory;
    private readonly ConcurrentDictionary<Guid, AgentTaskState> _inMemoryStates;
    private readonly ILogger<AgentTaskStateService> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AgentTaskStateService(
        IFileSystem? fileSystem = null,
        string? stateDirectory = null,
        ILogger<AgentTaskStateService>? logger = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        _stateDirectory = stateDirectory ?? Path.Combine(Path.GetTempPath(), "OpenLMStudio", "states");
        _inMemoryStates = new ConcurrentDictionary<Guid, AgentTaskState>();
        _logger = logger ?? NullLogger<AgentTaskStateService>.Instance;
    }

    public void Dispose()
    {
        _inMemoryStates.Clear();
    }

    public async Task<AgentTaskState?> GetStateAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            // Check in-memory first
            if (_inMemoryStates.TryGetValue(taskId, out var state))
                return state;

            // Load from file
            var statePath = GetStateFilePath(taskId);
            if (!_fileSystem.File.Exists(statePath))
                return null;

            var json = await _fileSystem.File.ReadAllTextAsync(statePath, ct);
            var loadedState = JsonSerializer.Deserialize<AgentTaskState>(json);

            if (loadedState != null)
            {
                _inMemoryStates[taskId] = loadedState;
            }

            return loadedState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting state for task {TaskId}", taskId);
            return null;
        }
    }

    public async Task SaveStateAsync(Guid taskId, AgentTaskState state, CancellationToken ct = default)
    {
        try
        {
            state.UpdatedAt = DateTime.UtcNow;

            var statePath = GetStateFilePath(taskId);
            var dir = _fileSystem.Path.GetDirectoryName(statePath);
            if (!string.IsNullOrEmpty(dir))
                _fileSystem.Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await _fileSystem.File.WriteAllTextAsync(statePath, json, ct);

            lock (_lock)
            {
                _inMemoryStates[taskId] = state;
            }

            _logger?.LogDebug("State saved for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving state for task {TaskId}", taskId);
            throw;
        }
    }

    public async Task UpdateAgentStateAsync(Guid taskId, AgentStateExtended newState, CancellationToken ct = default)
    {
        var state = await GetStateAsync(taskId, ct);
        if (state != null)
        {
            state.AgentState = newState;
            state.UpdatedAt = DateTime.UtcNow;
            await SaveStateAsync(taskId, state, ct);
        }
    }

    public async Task ResetStateAsync(Guid taskId, CancellationToken ct = default)
    {
        var state = await GetStateAsync(taskId, ct);
        if (state != null)
        {
            state.Reset();
            await SaveStateAsync(taskId, state, ct);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, AgentTaskState>> GetAllStatesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new Dictionary<Guid, AgentTaskState>();

            foreach (var kvp in _inMemoryStates)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all states");
            return new Dictionary<Guid, AgentTaskState>().AsReadOnly();
        }
    }

    private string GetStateFilePath(Guid taskId)
    {
        return Path.Combine(_stateDirectory, $"{taskId:N}.json");
    }
}