using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Defines the phases of agent communication for structured plan/act cycles.
/// </summary>
public enum AgentCommunicationPhase
{
    /// <summary>Agent is analyzing the task and proposing a plan.</summary>
    Planning,
    /// <summary>Agent is executing actions based on the approved plan.</summary>
    Acting,
    /// <summary>Agent is reviewing results and determining completion.</summary>
    Reviewing,
    /// <summary>Agent has completed the task successfully.</summary>
    Completed,
    /// <summary>Agent encountered an unrecoverable error.</summary>
    Failed
}

/// <summary>
/// Message exchanged between agent and user during the plan/act cycle.
/// </summary>
public record AgentCommunicationMessage(
    Guid Id,
    AgentCommunicationPhase Phase,
    string Content,
    string SenderRole,
    IReadOnlyDictionary<string, object> Metadata,
    DateTime Timestamp);

/// <summary>
/// Event raised when the agent transitions between plan and act phases.
/// </summary>
public record PhaseTransitionEvent(
    Guid TaskId,
    AgentCommunicationPhase From,
    AgentCommunicationPhase To,
    string Reason);

/// <summary>
/// Handles the agent communication protocol for plan/act switches.
/// Supports user approval gating between phases, auto-commit for safe operations,
/// and phase transition event system with listeners.
/// </summary>
public class AgentCommunicationProtocol : IDisposable
{
    private readonly ILogger<AgentCommunicationProtocol>? _logger;
    private readonly HashSet<Action<PhaseTransitionEvent>> _listeners = new();
    private bool _disposed;
    private readonly Dictionary<string, string> _safeOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        { "FileReadTool", "Read-only operation" },
        { "SearchFilesTool", "Read-only operation" },
        { "GitHistoryTool", "Read-only operation" },
        { "GitDiffTool", "Read-only operation" },
        { "ProjectExplorerTool", "Read-only operation" }
    };

    public AgentCommunicationProtocol(ILogger<AgentCommunicationProtocol>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a listener for phase transition events.
    /// </summary>
    public void Subscribe(Action<PhaseTransitionEvent> listener)
    {
        if (_disposed) return;
        _listeners.Add(listener);
    }

    /// <summary>
    /// Unregisters a phase transition listener.
    /// </summary>
    public void Unsubscribe(Action<PhaseTransitionEvent> listener)
    {
        _listeners.Remove(listener);
    }

    /// <summary>
    /// Generates a structured plan message from the LLM response.
    /// Parses the plan into phases and steps.
    /// </summary>
    public AgentCommunicationMessage CreatePlanMessage(
        Guid taskId,
        string description,
        string planContent)
    {
        var steps = ExtractPlanSteps(planContent);
        var metadata = new Dictionary<string, object>
        {
            ["StepCount"] = steps.Count,
            ["Description"] = description
        };

        return new AgentCommunicationMessage(
            Id: Guid.NewGuid(),
            Phase: AgentCommunicationPhase.Planning,
            Content: planContent,
            SenderRole: "agent",
            Metadata: metadata,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Generates a structured action message from tool execution results.
    /// </summary>
    public AgentCommunicationMessage CreateActionMessage(
        Guid taskId,
        string toolName,
        string result,
        bool success)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ToolName"] = toolName,
            ["Success"] = success
        };

        return new AgentCommunicationMessage(
            Id: Guid.NewGuid(),
            Phase: AgentCommunicationPhase.Acting,
            Content: result,
            SenderRole: "agent",
            Metadata: metadata,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Checks if a tool operation is considered "safe" and can be auto-approved
    /// without user review.
    /// </summary>
    public bool IsSafeOperation(string toolName)
    {
        return _safeOperations.ContainsKey(toolName);
    }

    /// <summary>
    /// Determines if user approval is required before transitioning to a new phase.
    /// Unsafe operations always require approval; safe operations auto-approve.
    /// </summary>
    public bool RequiresUserApproval(string toolName)
    {
        return !IsSafeOperation(toolName);
    }

    /// <summary>
    /// Raises a phase transition event to all listeners.
    /// </summary>
    public void RaisePhaseTransition(PhaseTransitionEvent e)
    {
        if (_disposed) return;

        _logger?.LogInformation(
            "[PhaseTransition] Task {TaskId}: {From} -> {To} ({Reason})",
            e.TaskId, e.From, e.To, e.Reason);

        foreach (var listener in _listeners)
        {
            try
            {
                listener(e);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in phase transition listener");
            }
        }
    }

    /// <summary>
    /// Creates a completion summary message.
    /// </summary>
    public AgentCommunicationMessage CreateCompletionMessage(
        Guid taskId,
        string summary,
        int totalToolCalls,
        IReadOnlyList<AgentToolCallRecord> toolCalls)
    {
        var metadata = new Dictionary<string, object>
        {
            ["TotalToolCalls"] = totalToolCalls,
            ["SuccessfulCalls"] = toolCalls.Count(c => c.Success)
        };

        return new AgentCommunicationMessage(
            Id: Guid.NewGuid(),
            Phase: AgentCommunicationPhase.Completed,
            Content: summary,
            SenderRole: "agent",
            Metadata: metadata,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a failure message with diagnostic information.
    /// </summary>
    public AgentCommunicationMessage CreateFailureMessage(
        Guid taskId,
        string error,
        IReadOnlyList<AgentToolCallRecord> toolCalls)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ErrorMessage"] = error,
            ["TotalToolCalls"] = toolCalls.Count,
            ["FailedCalls"] = toolCalls.Count(c => !c.Success)
        };

        return new AgentCommunicationMessage(
            Id: Guid.NewGuid(),
            Phase: AgentCommunicationPhase.Failed,
            Content: error,
            SenderRole: "agent",
            Metadata: metadata,
            Timestamp: DateTime.UtcNow);
    }

    private static List<string> ExtractPlanSteps(string plan)
    {
        var steps = new List<string>();
        var lines = plan.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Recognize numbered/bulleted steps
            if ((trimmed.StartsWith("- ") || trimmed.StartsWith("* ") ||
                 trimmed.StartsWith("1.") || trimmed.StartsWith("2.") ||
                 trimmed.StartsWith("3.") || trimmed.StartsWith("4.") ||
                 trimmed.StartsWith("5.")))
            {
                steps.Add(trimmed[2..]);
            }
        }
        return steps;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _listeners.Clear();
            _disposed = true;
        }
    }
}