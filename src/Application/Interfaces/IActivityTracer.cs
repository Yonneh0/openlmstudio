using System.Collections.Generic;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Records a single agent tool call event for tracing.
/// </summary>
public record AgentToolCallTrace(
    string TaskId,
    string ToolName,
    string ToolVersion,
    double DurationMs,
    bool Success,
    string? Error,
    long CpuTimeMs,
    long MemoryDeltaBytes,
    long DiskDeltaBytes,
    System.DateTimeOffset RecordedAt);

/// <summary>
/// Service for tracing agent tool calls and activity events.
/// Records duration, success/failure, and resource consumption per call.
/// </summary>
public interface IActivityTracer : System.IDisposable
{
    /// <summary>
    /// Records a tool call event with timing and resource consumption.
    /// </summary>
    void RecordToolCall(AgentToolCallTrace trace);

    /// <summary>
    /// Gets recent tool call traces for a task.
    /// </summary>
    IReadOnlyList<AgentToolCallTrace> GetRecentToolCalls(string taskId, int count = 50);

    /// <summary>
    /// Gets overall statistics for a task.
    /// </summary>
    TaskCallStatistics GetTaskStatistics(string taskId);

    /// <summary>
    /// Starts a new activity span for tracking.
    /// </summary>
    System.Diagnostics.Activity? StartSpan(string operationName, string? taskId = null, System.Diagnostics.ActivityKind kind = System.Diagnostics.ActivityKind.Internal);

    /// <summary>
    /// Completes a span with success or failure status.
    /// </summary>
    void CompleteSpan(System.Diagnostics.Activity? span, bool success = true);
}

/// <summary>
/// Aggregate statistics for a set of task calls.
/// </summary>
public record TaskCallStatistics(
    string TaskId,
    int TotalCalls,
    int SuccessfulCalls,
    int FailedCalls,
    double AverageDurationMs,
    double TotalDurationMs,
    long TotalCpuTimeMs,
    long TotalMemoryDeltaBytes,
    long TotalDiskDeltaBytes);