using System.Collections.Concurrent;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tracks agentic task progress through stages with iteration limit enforcement.
/// </summary>
public class TaskProgressTracker : ITaskProgressTracker, IDisposable
{
    private readonly ConcurrentDictionary<string, TrackerEntry> _entries = new();

    public int ProgressPercentage { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }

    public int IterationCount { get; set; }

    public bool IsIterationLimitExceeded => IterationCount >= 50; // Default max iterations for the task

    private TrackerEntry? GetActive()
    {
        foreach (var entry in _entries.Values)
            if (!entry.Completed)
                return entry;
        return null;
    }

    public TaskProgressStage Stage => HasError || IsIterationLimitExceeded ? TaskProgressStage.Failed : 
        ProgressPercentage == 100 ? TaskProgressStage.Completed : 
        GetActive() is { Completed: true } ? TaskProgressStage.Reviewing : TaskProgressStage.InProgress;

    public async Task UpdateStageAsync(TaskProgressStage newStage)
    {
        if (newStage == TaskProgressStage.Failed && !HasError)
            HasError = true;

        ProgressPercentage = newStage switch
        {
            TaskProgressStage.Completed => 100,
            TaskProgressStage.Reviewing => Math.Clamp(ProgressPercentage + 25, 0, 99),
            _ => Math.Clamp(ProgressPercentage + 10, 0, 99)
        };

        // Update the active entry's stage
        var active = GetActive();
        if (!HasError && active != null)
        {
            if (newStage == TaskProgressStage.Completed)
                active.Completed = true;
            else if (newStage == TaskProgressStage.Failed)
            {
                HasError = true;
                ErrorMessage ??= "Task failed";
            }
            else if (!active.Completed)
            {
                // Reviewing stage — don't change Completed/Failed flags, just update progress
            }
        }
    }

    public async Task ReportProgressAsync(int percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Progress must be between 0 and 100.");

        ProgressPercentage = percentage;

        var active = GetActive();
        if (active != null && !HasError && percentage == 100)
            active.Completed = true;
    }

    public async Task RecordToolCallAsync(string toolName, Dictionary<string, object> parameters, string result, bool success, double durationMs)
    {
        if (!success && !HasError)
        {
            HasError = true;
            ErrorMessage = $"Tool '{toolName}' failed after {durationMs:F1}ms with result: {result}";
        }

        // Increment iteration count on each tool call for loop detection
        IterationCount++;
        if (IsIterationLimitExceeded && !HasError)
            HasError = true;

        // Record to underlying tracker entry — handled by iteration count increment above
    }

    public async Task RecordErrorAsync(string errorMessage)
    {
        HasError = true;
        ErrorMessage = errorMessage;
        ProgressPercentage = 0;
    }

    public void Dispose() { _entries.Clear(); }

    private sealed class TrackerEntry : IDisposable
    {
        public bool Completed { get; internal set; }

        public void Dispose() { /* No unmanaged resources */ }
    }
}
