global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Linq;
global using Microsoft.Extensions.Logging;
global using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Traces agent tool calls including duration, success/failure, and resource consumption per call.
/// Records context compression events and integrates with ModelLifecycleTracer.
/// </summary>
public class ActivityTracer : IActivityTracer, IDisposable
{
    private readonly ILogger<ActivityTracer>? _logger;
    private readonly ConcurrentDictionary<string, List<AgentToolCallTrace>> _taskTraces = new();
    private readonly ActivitySource _activitySource = new("OpenLMStudio.ActivityTracer");
    private bool _disposed;

    public ActivityTracer(ILogger<ActivityTracer>? logger = null)
    {
        _logger = logger;
    }

    public void RecordToolCall(AgentToolCallTrace trace)
    {
        if (_disposed) return;

        var traces = _taskTraces.GetOrAdd(trace.TaskId, _ => new());
        lock (traces)
        {
            traces.Add(trace);
        }

        _logger?.LogDebug("[Activity] {TaskId} {ToolName}: {Duration:F1}ms {Status} (CPU: {Cpu}ms, Mem: {MemDelta:+0;-0})",
            trace.TaskId, trace.ToolName, trace.DurationMs, trace.Success ? "OK" : $"FAIL ({trace.Error})",
            trace.CpuTimeMs, trace.MemoryDeltaBytes);
    }

    public IReadOnlyList<AgentToolCallTrace> GetRecentToolCalls(string taskId, int count = 50)
    {
        if (!_taskTraces.TryGetValue(taskId, out var traces))
            return Array.Empty<AgentToolCallTrace>();

        lock (traces)
            return traces.Skip(Math.Max(0, traces.Count - count)).ToList();
    }

    public TaskCallStatistics GetTaskStatistics(string taskId)
    {
        if (!_taskTraces.TryGetValue(taskId, out var traces))
            return new TaskCallStatistics(taskId, 0, 0, 0, 0, 0, 0, 0, 0);

        lock (traces)
        {
            var total = traces.Count;
            var successful = traces.Count(t => t.Success);
            var failed = total - successful;
            var avgDuration = traces.Any() ? traces.Average(t => t.DurationMs) : 0;
            var totalDuration = traces.Sum(t => t.DurationMs);
            var totalCpu = traces.Sum(t => t.CpuTimeMs);
            var totalMem = traces.Sum(t => t.MemoryDeltaBytes);
            var totalDisk = traces.Sum(t => t.DiskDeltaBytes);

            return new TaskCallStatistics(taskId, total, successful, failed, avgDuration, totalDuration, totalCpu, totalMem, totalDisk);
        }
    }

    public Activity? StartSpan(string operationName, string? taskId = null, ActivityKind kind = ActivityKind.Internal)
    {
        var span = _activitySource.StartActivity(operationName, kind, parentContext: default,
            tags: taskId is not null ? new Dictionary<string, object?> { ["agent.task.id"] = taskId } : null);
        return span;
    }

    public void CompleteSpan(Activity? span, bool success = true)
    {
        if (span == null) return;

        span.SetTag("agent.success", success);
        if (!success)
            span.SetTag("agent.error", span.Status.ToString());

        span.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
