namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a checkpoint saved during agent task execution.
/// </summary>
public class TaskCheckpoint
{
    /// <summary>Unique identifier for this checkpoint.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Task ID this checkpoint belongs to.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Checkpoint version.</summary>
    public int Version { get; set; }

    /// <summary>Checkpoint timestamp.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Checkpoint type (Tool, Task, or Completion).</summary>
    public CheckpointType Type { get; set; }

    /// <summary>Name of the tool that created this checkpoint.</summary>
    public string? ToolName { get; set; }

    /// <summary>Tool parameters at the time of checkpoint.</summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>Tool result at the time of checkpoint.</summary>
    public string? Result { get; set; }

    /// <summary>Agent state at the time of checkpoint.</summary>
    public AgentStateExtended AgentState { get; set; } = AgentStateExtended.Idle;

    /// <summary>Agent task state at the time of checkpoint.</summary>
    public AgentTaskState? AgentTaskState { get; set; }

    /// <summary>Whether this checkpoint was saved during an attempt_completion.</summary>
    public bool IsCompletionCheckpoint { get; set; }

    /// <summary>Completion message timestamp, if this is a completion checkpoint.</summary>
    public DateTime? CompletionMessageTimestamp { get; set; }

    /// <summary>
    /// Creates a tool checkpoint.
    /// </summary>
    public static TaskCheckpoint CreateToolCheckpoint(Guid taskId, string toolName, Dictionary<string, object> parameters, string result)
    {
        return new TaskCheckpoint
        {
            TaskId = taskId,
            Type = CheckpointType.Tool,
            ToolName = toolName,
            Parameters = parameters,
            Result = result,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a task checkpoint.
    /// </summary>
    public static TaskCheckpoint CreateTaskCheckpoint(Guid taskId)
    {
        return new TaskCheckpoint
        {
            TaskId = taskId,
            Type = CheckpointType.Task,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a completion checkpoint.
    /// </summary>
    public static TaskCheckpoint CreateCompletionCheckpoint(Guid taskId)
    {
        return new TaskCheckpoint
        {
            TaskId = taskId,
            Type = CheckpointType.Completion,
            IsCompletionCheckpoint = true,
            CompletionMessageTimestamp = DateTime.UtcNow,
            Timestamp = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// Types of checkpoints in the agent task system.
/// </summary>
public enum CheckpointType
{
    /// <summary>Checkpoint saved after each tool call.</summary>
    Tool,

    /// <summary>Checkpoint saved after each task step.</summary>
    Task,

    /// <summary>Checkpoint saved after task completion.</summary>
    Completion,
}