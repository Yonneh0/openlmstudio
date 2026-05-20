global using System;
global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Runtime.CompilerServices;
global using Microsoft.Extensions.Logging;
global using MLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenLMStudio.Infrastructure.Tracing;

/// <summary>
/// No-op ILogger for when DI is not configured.
/// </summary>
internal class NoOpLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(MLogLevel logLevel) => true;
    public void Log<TState>(MLogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

/// <summary>
/// Traces model lifecycle events including load/unload times and VRAM allocation changes.
/// Tracks per-model timing and resource consumption.
/// </summary>
public class ModelLifecycleTracer : IDisposable
{
    private readonly ILogger<ModelLifecycleTracer>? _logger;
    private readonly ConcurrentDictionary<string, ModelLoadTrace> _activeLoads = new();
    private readonly ConcurrentQueue<ModelLoadTrace> _recentTraces = new();
    private bool _disposed;

    public ModelLifecycleTracer(ILogger<ModelLifecycleTracer>? logger = null)
    {
        _logger = logger ?? new NoOpLogger<ModelLifecycleTracer>();
    }

    public int ActiveLoadCount => _activeLoads.Count;
    public int TracedModels => _recentTraces.Count;

    public void TrackModelLoad(string modelId, string modelType, long fileSizeBytes, [CallerMemberName] string caller = "")
    {
        if (_disposed) return;

        var trace = new ModelLoadTrace
        {
            ModelId = modelId,
            ModelType = modelType,
            FileSizeBytes = fileSizeBytes,
            StartedAt = DateTime.UtcNow,
            Caller = caller
        };

        _activeLoads[modelId] = trace;
        _logger?.LogDebug("[Lifecycle] Load started: {ModelId} ({ModelType}) from {Caller}", modelId, modelType, caller);
    }

    public void CompleteModelLoad(string modelId, float loadTimeMs, long vramAllocated, bool success)
    {
        if (_disposed) return;

        if (!_activeLoads.TryRemove(modelId, out var trace)) return;

        trace.CompletedAt = DateTime.UtcNow;
        trace.LoadTimeMs = loadTimeMs;
        trace.VramAllocatedBytes = vramAllocated;
        trace.Success = success;

        _recentTraces.Enqueue(trace);

        // Keep only last 100 traces
        while (_recentTraces.Count > 100)
        {
            _recentTraces.TryDequeue(out _);
        }

        if (success)
        {
            _logger?.LogInformation("[Lifecycle] Load complete: {ModelId} in {Time:F1}ms (VRAM: {Vram:N0} bytes)",
                modelId, loadTimeMs, vramAllocated);
        }
        else
        {
            _logger?.LogWarning("[Lifecycle] Load failed: {ModelId} ({Time:F1}ms)", modelId, loadTimeMs);
        }
    }

    public void TrackModelUnload(string modelId)
    {
        if (_disposed) return;
        _logger?.LogDebug("[Lifecycle] Unload started: {ModelId}", modelId);
    }

    public IReadOnlyList<ModelLoadTrace> GetRecentTraces(int count = 20)
    {
        return _recentTraces.Take(count).ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    public record ModelLoadTrace
    {
        public string ModelId { get; set; } = "";
        public string ModelType { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public float? LoadTimeMs { get; set; }
        public long? VramAllocatedBytes { get; set; }
        public bool? Success { get; set; }
        public string Caller { get; set; } = "";
    }
}