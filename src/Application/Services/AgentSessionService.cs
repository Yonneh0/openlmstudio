namespace OpenLMStudio.Application.Services.Agent;

using OpenLMStudio.Domain.Models;

/// <summary>
/// Manages agent session lifecycle.
/// Creates, updates, and disposes agent sessions.
/// </summary>
public class AgentSessionService
{
    private readonly List<AgentSession> _sessions = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new agent session.
    /// </summary>
    public AgentSession CreateSession(string workingDirectory = "")
    {
        var session = new AgentSession { WorkingDirectory = workingDirectory };
        lock (_lock)
            _sessions.Add(session);
        return session;
    }

    /// <summary>
    /// Gets an existing session by ID, or creates a new one.
    /// </summary>
    public AgentSession GetOrCreateSession(Guid? sessionId = null)
    {
        if (sessionId.HasValue)
        {
            lock (_lock)
            {
                var existing = _sessions.FirstOrDefault(s => s.Id == sessionId.Value);
                if (existing != null)
                    return existing;
            }
        }
        return CreateSession();
    }

    /// <summary>
    /// Gets the active session (Planning or Acting).
    /// </summary>
    public AgentSession? GetActiveSession()
    {
        lock (_lock)
            return _sessions.FirstOrDefault(s => s.IsActive);
    }

    /// <summary>
    /// Pauses the active session.
    /// </summary>
    public void PauseActiveSession()
    {
        lock (_lock)
        {
            var active = _sessions.FirstOrDefault(s => s.IsActive);
            active?.UpdateState(AgentState.Paused);
        }
    }

    /// <summary>
    /// Resumes the paused session.
    /// </summary>
    public void ResumeSession(Guid sessionId)
    {
        lock (_lock)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            session?.UpdateState(Domain.Models.AgentState.Acting);
        }
    }

    /// <summary>
    /// Completes the active session.
    /// </summary>
    public void CompleteActiveSession()
    {
        lock (_lock)
        {
            var active = _sessions.FirstOrDefault(s => s.IsActive);
            active?.UpdateState(Domain.Models.AgentState.Completed);
        }
    }

    /// <summary>
    /// Disposes a session.
    /// </summary>
    public void DisposeSession(Guid sessionId)
    {
        lock (_lock)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            session?.Dispose();
            _sessions.Remove(session);
        }
    }

    /// <summary>
    /// Lists all sessions.
    /// </summary>
    public IReadOnlyList<AgentSession> ListSessions()
    {
        lock (_lock)
            return _sessions.ToList().AsReadOnly();
    }
}
