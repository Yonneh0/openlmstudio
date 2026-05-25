namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents the current state of an agent task with all tracking fields.
/// </summary>
public class AgentTaskState
{
    /// <summary>Unique task ID.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Unique task identifier (ULID).</summary>
    public string Ulid { get; set; } = string.Empty;

    /// <summary>Current mode (Plan or Act).</summary>
    public string Mode { get; set; } = "Act";

    /// <summary>Current task progress tracking.</summary>
    public AgentTaskProgress? TaskProgress { get; set; }

    /// <summary>Range of deleted conversation history.</summary>
    public (int? Start, int? End)? ConversationHistoryDeletedRange { get; set; }

    /// <summary>Cache for file reads.</summary>
    public Dictionary<string, string> FileReadCache { get; set; } = new();

    /// <summary>Count of consecutive mistakes.</summary>
    public int ConsecutiveMistakeCount { get; set; }

    /// <summary>Flag indicating if a tool was rejected.</summary>
    public bool DidRejectTool { get; set; }

    /// <summary>Flag indicating if a file was edited.</summary>
    public bool DidEditFile { get; set; }

    /// <summary>Flag indicating if a tool was already used.</summary>
    public bool DidAlreadyUseTool { get; set; }

    /// <summary>Flag indicating if the task is awaiting a plan response.</summary>
    public bool IsAwaitingPlanResponse { get; set; }

    /// <summary>Flag indicating if the task responded to a plan ask by switching mode.</summary>
    public bool DidRespondToPlanAskBySwitchingMode { get; set; }

    /// <summary>Flag indicating if double-check completion is pending.</summary>
    public bool DoubleCheckCompletionPending { get; set; }

    /// <summary>Flag indicating if the task is currently summarizing.</summary>
    public bool CurrentlySummarizing { get; set; }

    /// <summary>Name of the last tool used.</summary>
    public string? LastToolName { get; set; }

    /// <summary>Content of the user message.</summary>
    public string? UserMessageContent { get; set; }

    /// <summary>Active hook execution.</summary>
    public string? ActiveHookExecution { get; set; }

    /// <summary>Flag indicating if the task is aborting.</summary>
    public bool Abort { get; set; }

    /// <summary>Current agent state.</summary>
    public AgentStateExtended AgentState { get; set; } = AgentStateExtended.Idle;

    /// <summary>Timestamp when the state was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Resets the state to a clean initial state for a new task.
    /// </summary>
    public void Reset()
    {
        TaskProgress = new AgentTaskProgress();
        ConversationHistoryDeletedRange = null;
        FileReadCache.Clear();
        ConsecutiveMistakeCount = 0;
        DidRejectTool = false;
        DidEditFile = false;
        DidAlreadyUseTool = false;
        IsAwaitingPlanResponse = false;
        DidRespondToPlanAskBySwitchingMode = false;
        DoubleCheckCompletionPending = false;
        CurrentlySummarizing = false;
        LastToolName = null;
        UserMessageContent = null;
        ActiveHookExecution = null;
        Abort = false;
        AgentState = AgentStateExtended.Idle;
        UpdatedAt = DateTime.UtcNow;
    }
}