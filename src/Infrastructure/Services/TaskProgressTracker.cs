using System.Collections.Concurrent;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tracks agentic task progress through stages with iteration limit enforcement.
/// </summary>
public class TaskProgressTracker : ITaskProgressTracker, IDisposable
{
    private readonly ConcurrentDictionary<string, TrackerEntry> _entries = new();
    private int _progressPercentage;
    private int _hasError;
    private int _iterationCount;

    public int ProgressPercentage => _progressPercentage;

    public bool HasError => _hasError != 0;

    public string? ErrorMessage { get; set; }

    public int IterationCount => _iterationCount;

    public bool IsIterationLimitExceeded => _iterationCount >= 50; // Default max iterations for the task

    private TrackerEntry? GetActive()
    {
        foreach (var entry in _entries.Values.ToList())
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
            Interlocked.Exchange(ref _hasError, 1);

        var newProgress = newStage switch
        {
            TaskProgressStage.Completed => 100,
            TaskProgressStage.Reviewing => Math.Clamp(_progressPercentage + 25, 0, 99),
            _ => Math.Clamp(_progressPercentage + 10, 0, 99)
        };
        Interlocked.Exchange(ref _progressPercentage, newProgress);

        // Update the active entry's stage
        var active = GetActive();
        if (!HasError && active != null)
        {
            if (newStage == TaskProgressStage.Completed)
                active.Completed = true;
            else if (newStage == TaskProgressStage.Failed)
            {
                Interlocked.Exchange(ref _hasError, 1);
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

        Interlocked.Exchange(ref _progressPercentage, percentage);

        var active = GetActive();
        if (active != null && !HasError && percentage == 100)
            active.Completed = true;
    }

    public async Task RecordToolCallAsync(string toolName, Dictionary<string, object> parameters, string result, bool success, double durationMs)
    {
        if (!success && !HasError)
        {
            Interlocked.Exchange(ref _hasError, 1);
            ErrorMessage = $"Tool '{toolName}' failed after {durationMs:F1}ms with result: {result}";
        }

        // Increment iteration count on each tool call for loop detection
        Interlocked.Increment(ref _iterationCount);
        if (IsIterationLimitExceeded && !HasError)
            Interlocked.Exchange(ref _hasError, 1);

        // Record to underlying tracker entry — handled by iteration count increment above
    }

    public async Task RecordErrorAsync(string errorMessage)
    {
        Interlocked.Exchange(ref _hasError, 1);
        ErrorMessage = errorMessage;
        Interlocked.Exchange(ref _progressPercentage, 0);
    }

    public void Dispose() { _entries.Clear(); }

    private sealed class TrackerEntry : IDisposable
    {
        public bool Completed { get; internal set; }

        public void Dispose() { /* No unmanaged resources */ }
    }
}