using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tracks agentic task progress through stages, reports iteration limits and errors.
/// </summary>
public class AgentTaskProgressTracker : ITaskProgressTracker
{
    private readonly ILogger<AgentTaskProgressTracker>? _logger;
    private TaskProgressStage _stage = TaskProgressStage.NotStarted;
    private int _progressPercentage = 0;
    private int _iterationsExecuted = 0;
    private bool _isIterationLimitExceeded = false;
    private readonly List<AgentToolCallRecord> _toolCalls = new();
    private string? _errorMessage;

    public AgentTaskProgressTracker(ILogger<AgentTaskProgressTracker>? logger = null)
    {
        _logger = logger;
    }

    public TaskProgressStage Stage => _stage;

    public int ProgressPercentage => _progressPercentage;

    public bool IsIterationLimitExceeded => _isIterationLimitExceeded;

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public string? ErrorMessage => _errorMessage;

    public async Task UpdateStageAsync(TaskProgressStage newStage)
    {
        var old = _stage;
        _stage = newStage;

        // Auto-update progress based on stage
        switch (newStage)
        {
            case TaskProgressStage.NotStarted:
                _progressPercentage = 0;
                break;
            case TaskProgressStage.InProgress:
                _progressPercentage = Math.Min(_progressPercentage, 85); // Can't be >85% while still in progress
                break;
            case TaskProgressStage.Reviewing:
                _progressPercentage = 90;
                break;
            case TaskProgressStage.Completed:
                _progressPercentage = 100;
                break;
            case TaskProgressStage.Failed:
                _progressPercentage = 0; // Failed = no progress
                break;
        }

        _logger?.LogInformation("Task progress stage updated: {Old} -> {New}", old, newStage);
    }

    public async Task ReportProgressAsync(int percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0 and 100.");

        _progressPercentage = percentage;
        _logger?.LogDebug("Task progress reported: {Percentage}%", percentage);
    }

    public async Task RecordToolCallAsync(string toolName, Dictionary<string, object> parameters, string result, bool success, double durationMs)
    {
        var record = new AgentToolCallRecord(toolName, parameters, result, success, durationMs, DateTime.UtcNow);
        _toolCalls.Add(record);

        // Check if the tool call took too long (>5 minutes is likely a stuck operation)
        if (durationMs > 300_000 && !success)
        {
            var errorMessage = $"Tool '{toolName}' timed out after {(durationMs / 1000):F0} seconds.";
            _logger?.LogWarning("Agent tool call timeout: {ErrorMessage}", errorMessage);
            await RecordErrorAsync(errorMessage);
        }

        // Check if there are too many consecutive failed tool calls (likely stuck in a loop)
        var recentFailures = _toolCalls.Where(c => !c.Success && (DateTime.UtcNow - c.Timestamp).TotalSeconds < 60)
                                        .Count();
        if (recentFailures > 10)
        {
            var errorMessage = $"Too many consecutive tool call failures ({recentFailures} in last 60 seconds). Agent may be stuck.";
            _logger?.LogWarning("Agent stuck loop detected: {ErrorMessage}", errorMessage);
            await RecordErrorAsync(errorMessage);
        }

        _logger?.LogDebug("Tool call recorded: {ToolName} - Success={Success}, DurationMs={DurationMs:F1}",
            toolName, success, durationMs);
    }

    public async Task RecordErrorAsync(string errorMessage)
    {
        _errorMessage = errorMessage;
        await UpdateStageAsync(TaskProgressStage.Failed);
        _logger?.LogWarning("Agent error recorded: {ErrorMessage}", errorMessage);
    }

    /// <summary>
    /// Checks if the agent has exceeded its iteration limit and should stop.
    /// </summary>
    public void CheckIterationLimit(int maxIterations)
    {
        _iterationsExecuted++;
        if (_iterationsExecuted > maxIterations && !_isIterationLimitExceeded)
        {
            _isIterationLimitExceeded = true;
            var errorMessage = $"Agent exceeded iteration limit of {maxIterations}. Stopping execution.";
            _logger?.LogWarning("Iteration limit reached: {MaxIterations}", maxIterations);
            _errorMessage = errorMessage;
        }

        // Update progress to 85% when stuck in progress state (agent is working but not making visible progress)
        if (_isIterationLimitExceeded && _progressPercentage != 85)
        {
            _progressPercentage = 85;
        }
    }

    public IReadOnlyList<AgentToolCallRecord> GetToolCalls() => _toolCalls.AsReadOnly();

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}