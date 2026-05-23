using System;

namespace OpenLMStudio.Application.Types;

/// <summary>
/// Result of a performance benchmark run.
/// </summary>
public class BenchmarkResult
{
    public string BenchmarkName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public double DurationMs { get; set; }
    public double DurationSec => DurationMs / 1000.0;
    public bool Succeeded { get; set; }
    public string? Error { get; set; }

    /// <summary>Model loading time in milliseconds.</summary>
    public double ModelLoadTimeMs { get; set; }

    /// <summary>Average tokens per second (for chat benchmarks).</summary>
    public double TokensPerSecond { get; set; }

    /// <summary>Average latency in milliseconds (for image generation).</summary>
    public double AvgLatencyMs { get; set; }

    /// <summary>P50 latency in milliseconds.</summary>
    public double P50LatencyMs { get; set; }

    /// <summary>P95 latency in milliseconds.</summary>
    public double P95LatencyMs { get; set; }

    /// <summary>P99 latency in milliseconds.</summary>
    public double P99LatencyMs { get; set; }

    /// <summary>Peak memory usage in MB.</summary>
    public double PeakMemoryMb { get; set; }

    public static BenchmarkResult Create(string benchmarkName, double durationMs)
    {
        return new BenchmarkResult
        {
            BenchmarkName = benchmarkName,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            DurationMs = durationMs,
            Succeeded = true,
        };
    }
}