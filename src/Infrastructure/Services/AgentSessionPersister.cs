using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Persists and restores agent session state to/from disk for crash recovery.
/// Saves the current checkpoint, tool call history, and conversation state.
/// </summary>
public class AgentSessionPersister : IDisposable
{
    private readonly ILogger<AgentSessionPersister>? _logger;
    private readonly string _sessionDirectory;
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public AgentSessionPersister(ILogger<AgentSessionPersister>? logger, string sessionDirectory)
    {
        _logger = logger;
        _sessionDirectory = sessionDirectory;
        _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        if (!Directory.Exists(_sessionDirectory))
            Directory.CreateDirectory(_sessionDirectory);
    }

    /// <summary>
    /// Saves the agent session state to disk.
    /// Creates a unique session file per task.
    /// </summary>
    public async Task SaveSessionAsync(
        Guid taskId,
        AgentState state,
        IReadOnlyList<AgentToolCallRecord> toolCalls,
        IReadOnlyList<AgentMessageExchange> conversationHistory,
        AgentCheckpoint? checkpoint,
        CancellationToken ct = default)
    {
        if (_disposed) return;

        try
        {
            var sessionFile = GetSessionFilePath(taskId);
            var session = new
            {
                TaskId = taskId,
                State = state.ToString(),
                ToolCalls = toolCalls,
                ConversationHistory = conversationHistory,
                Checkpoint = checkpoint,
                SavedAt = DateTime.UtcNow.ToString("o"),
                LastCheckpointIteration = checkpoint?.Iteration ?? 0
            };

            var json = System.Text.Json.JsonSerializer.Serialize(session, _jsonOptions);
            var tempFile = sessionFile + ".tmp";

            await File.WriteAllTextAsync(tempFile, json, ct);
            File.Move(tempFile, sessionFile, overwrite: true);

            _logger?.LogInformation("Saved agent session for task {TaskId} at iteration {Iteration}",
                taskId, checkpoint?.Iteration ?? toolCalls.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save session for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Loads a previously saved session for a task.
    /// Returns null if no session exists or is corrupted.
    /// </summary>
    public async Task<AgentSessionData?> LoadSessionAsync(Guid taskId, CancellationToken ct = default)
    {
        if (_disposed) return null;

        try
        {
            var sessionFile = GetSessionFilePath(taskId);
            if (!File.Exists(sessionFile))
                return null;

            var json = await File.ReadAllTextAsync(sessionFile, ct);
            var session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(json, _jsonOptions);

            if (session == null)
                return null;

            var toolCalls = session.ToolCalls ?? new List<AgentToolCallRecord>();
            var conversationHistory = session.ConversationHistory ?? new List<AgentMessageExchange>();

            return new AgentSessionData(
                State: (AgentState)Enum.Parse(typeof(AgentState), session.State, ignoreCase: true),
                ToolCalls: toolCalls,
                ConversationHistory: conversationHistory,
                Checkpoint: session.Checkpoint);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load session for task {TaskId}", taskId);
            return null;
        }
    }

    /// <summary>
    /// Deletes a saved session from disk.
    /// </summary>
    public async Task DeleteSessionAsync(Guid taskId, CancellationToken ct = default)
    {
        if (_disposed) return;

        try
        {
            var sessionFile = GetSessionFilePath(taskId);
            if (File.Exists(sessionFile))
                File.Delete(sessionFile);

            _logger?.LogDebug("Deleted session for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete session for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Lists all saved session task IDs.
    /// </summary>
    public IReadOnlyList<Guid> ListSessions()
    {
        if (_disposed) return Array.Empty<Guid>();

        try
        {
            var result = new List<Guid>();
            foreach (var file in Directory.GetFiles(_sessionDirectory, "*.session.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = System.Text.Json.JsonSerializer.Deserialize<SessionData>(json, _jsonOptions);
                    if (data != null && Guid.TryParse(data.TaskId.ToString(), out var taskId))
                        result.Add(taskId);
                }
                catch
                {
                    // Skip corrupted files
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to list sessions");
            return Array.Empty<Guid>();
        }
    }

    private string GetSessionFilePath(Guid taskId)
        => Path.Combine(_sessionDirectory, $"{taskId:N}.session.json");

    private class SessionData
    {
        public Guid TaskId { get; set; }
        public string State { get; set; } = "Idle";
        public List<AgentToolCallRecord>? ToolCalls { get; set; }
        public List<AgentMessageExchange>? ConversationHistory { get; set; }
        public AgentCheckpoint? Checkpoint { get; set; }
        public string? SavedAt { get; set; }
        public int LastCheckpointIteration { get; set; }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a loaded agent session with state and context.
/// </summary>
public record AgentSessionData(
    AgentState State,
    IReadOnlyList<AgentToolCallRecord> ToolCalls,
    IReadOnlyList<AgentMessageExchange> ConversationHistory,
    AgentCheckpoint? Checkpoint);