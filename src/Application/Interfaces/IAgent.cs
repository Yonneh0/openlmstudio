using System.Collections.Generic;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a request to start an agentic task.
/// </summary>
public record AgentTaskRequest(
    Guid TaskId,
    string Description,
    IReadOnlyList<string> AvailableTools = null!,
    IReadOnlyList<ContextSegment>? InitialContext = null!,
    int MaxIterations = 50);

/// <summary>
/// Represents the result of an agentic task execution.
/// </summary>
public record AgentTaskResult(
    Guid TaskId,
    AgentState FinalState,
    IReadOnlyList<Domain.Models.AgentToolCallRecord> ToolCalls,
    string Summary);

/// <summary>
/// Represents an agent plan/act message exchange.
/// </summary>
public record AgentMessageExchange(
    Guid TaskId,
    string SenderRole, // "agent" or "user"
    string Phase,      // "planning" or "acting"
    string Content);

/// <summary>
/// Interface for managing the lifecycle of an agentic task.
/// </summary>
public interface IAgent : IDisposable
{
    /// <summary>
    /// Gets the current state of this agent task.
    /// </summary>
    AgentState State { get; }

    /// <summary>
    /// Starts executing the agent task with the given plan/act cycle.
    /// </summary>
    Task<AgentTaskResult> ExecuteAsync(AgentTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Pauses the agent task (e.g., awaiting user input/approval).
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Resumes a paused agent task.
    /// </summary>
    Task ResumeAsync(CancellationToken ct = default);

    /// <summary>
    /// Aborts the agent task immediately.
    /// </summary>
    Task AbortAsync();

    /// <summary>
    /// Gets all tool calls made during this agent's execution.
    /// </summary>
    IReadOnlyList<AgentToolCallRecord> GetToolCalls();

    /// <summary>
    /// Gets the conversation history for this agent task.
    /// </summary>
    IReadOnlyList<AgentMessageExchange> GetConversationHistory();
}

/// <summary>
/// Interface for tracking agentic task progress through stages.
/// </summary>
public interface ITaskProgressTracker : IDisposable
{
    /// <summary>
    /// Gets the current stage of a task.
    /// </summary>
    TaskProgressStage Stage { get; }

    /// <summary>
    /// Gets the percentage complete (0-100).
    /// </summary>
    int ProgressPercentage { get; }

    /// <summary>
    /// Updates the stage of a task.
    /// </summary>
    Task UpdateStageAsync(TaskProgressStage newStage);

    /// <summary>
    /// Reports progress update for a task (0-100).
    /// </summary>
    Task ReportProgressAsync(int percentage);

    /// <summary>
    /// Checks if the task has reached its iteration limit.
    /// </summary>
    bool IsIterationLimitExceeded { get; }

    /// <summary>
    /// Records an agent tool call for tracing purposes.
    /// </summary>
    Task RecordToolCallAsync(string toolName, Dictionary<string, object> parameters, string result, bool success, double durationMs);

    /// <summary>
    /// Checks if the task has hit an error condition (infinite loop, timeout).
    /// </summary>
    bool HasError { get; }

    /// <summary>
    /// Gets any error message associated with this task.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Records an error condition for the task.
    /// </summary>
    Task RecordErrorAsync(string errorMessage);
}

/// <summary>
/// Stages in a task's lifecycle tracked by ITaskProgressTracker.
/// </summary>
public enum TaskProgressStage
{
    /// <summary>Task has been created but not yet started.</summary>
    NotStarted,
    /// <summary>Task is actively being processed.</summary>
    InProgress,
    /// <summary>Reviewing a task result before proceeding.</summary>
    Reviewing,
    /// <summary>Task completed successfully.</summary>
    Completed,
    /// <summary>Task failed with an error.</summary>
    Failed
}

/// <summary>
/// Interface for agentic tool operations.
/// </summary>
public interface ITool : IDisposable
{
    /// <summary>
    /// Gets the name of this tool (e.g., "FileRead").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the tool with the given parameters.
    /// Returns true if the tool executed successfully, false otherwise.
    /// </summary>
    Task<bool> ExecuteAsync(Dictionary<string, object> parameters);

    /// <summary>
    /// Gets schema information about required and optional parameters for this tool.
    /// </summary>
    Dictionary<string, ToolParameterSchema> GetParameterSchema();
}

/// <summary>
/// Defines the schema for a parameter that can be passed to an ITool.
/// </summary>
public record ToolParameterSchema(
    string Type,  // "string", "number", "boolean"
    bool Required);

/// <summary>
/// Interface for the agent communication protocol — plan/act phase transitions with user approval gating.
/// </summary>
public interface IAgentProtocolService : IDisposable
{
    /// <summary>
    /// Gets the current state of the protocol.
    /// </summary>
    AgentState State { get; }

    /// <summary>
    /// Gets whether the current plan has been approved by the user.
    /// </summary>
    bool IsPlanApproved { get; }

    /// <summary>
    /// Gets the current plan content, if any.
    /// </summary>
    string? CurrentPlan { get; }

    /// <summary>
    /// Raised when the agent transitions between phases.
    /// </summary>
    event EventHandler<PhaseTransitionEventArgs>? PhaseTransitioned;

    /// <summary>
    /// Raised when user approval is required for a plan or action.
    /// </summary>
    event EventHandler<AgentApprovalRequiredEventArgs>? ApprovalRequired;

    /// <summary>
    /// Generates the initial plan for a task.
    /// </summary>
    Task<AgentPlan> GeneratePlanAsync(AgentTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Approves the current plan, transitioning to the acting phase.
    /// </summary>
    Task ApprovePlanAsync(Guid taskId);

    /// <summary>
    /// Rejects the current plan, requesting a new one.
    /// </summary>
    Task RejectPlanAsync(Guid taskId, string? reason = null);

    /// <summary>
    /// Checks if a proposed action is safe without user approval.
    /// </summary>
    Task<bool> IsActionSafeAsync(Guid taskId, string toolName, Dictionary<string, object> parameters);

    /// <summary>
    /// Requests user approval for a risky action.
    /// </summary>
    Task<bool> RequestActionApprovalAsync(Guid taskId, string toolName, Dictionary<string, object> parameters);
}

/// <summary>
/// Types of approval the agent may require from the user.
/// </summary>
public enum AgentApprovalType
{
    /// <summary>Plan approval — user approves the agent's approach before execution.</summary>
    Plan,
    /// <summary>Action approval — user approves a specific tool call.</summary>
    Action
}

/// <summary>
/// Represents an agent plan generated by the LLM.
/// </summary>
public record AgentPlan(
    Guid TaskId,
    string Content,
    DateTime GeneratedAt,
    bool IsApproved);

/// <summary>
/// Event arguments for agent phase transitions.
/// </summary>
public record PhaseTransitionEventArgs(
    Guid TaskId,
    AgentState OldState,
    AgentState NewState);

/// <summary>
/// Event arguments for agent approval requests.
/// </summary>
public record AgentApprovalRequiredEventArgs(
    Guid TaskId,
    AgentApprovalType ApprovalType,
    string Details);