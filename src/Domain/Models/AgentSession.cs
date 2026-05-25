namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents an agent's execution session with state tracking.
/// An agent session tracks the current state, tool calls, and context across multiple tool invocations.
/// </summary>
public class AgentSession : IDisposable
{
    /// <summary>Unique identifier for this session.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Current agent state (Idle, Planning, Acting, Paused, Completed, Failed).</summary>
    public AgentState State { get; set; } = AgentState.Idle;

    /// <summary>Whether this session is currently active.</summary>
    public bool IsActive => State is AgentState.Planning or AgentState.Acting;

    /// <summary>List of tool calls made during this session.</summary>
    public List<AgentToolCallRecord> ToolCalls { get; set; } = new();

    /// <summary>Current working directory for the session.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Context segments for this session.</summary>
    public List<ContextSegment> ContextSegments { get; set; } = new();

    /// <summary>Timestamp when the session was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Timestamp when the session was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the session has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Records a tool call in this session.
    /// </summary>
    public void RecordToolCall(AgentToolCallRecord record)
    {
        ToolCalls.Add(record);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the session state.
    /// </summary>
    public void UpdateState(AgentState newState)
    {
        State = newState;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds context segments to this session.
    /// </summary>
    public void AddContext(IEnumerable<ContextSegment> segments)
    {
        ContextSegments.AddRange(segments);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            State = AgentState.Idle;
        }
    }
}