using System;

namespace OpenLMStudio.Application.Types;

/// <summary>
/// Result of a server load test run.
/// </summary>
public class LoadTestResult
{
    public string TestName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public double DurationMs { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double DurationSec => DurationMs / 1000.0;
    public double RequestsPerSecond => DurationSec > 0 ? TotalRequests / DurationSec : 0;
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double AvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double ErrorRatePercent => TotalRequests > 0 ? (double)FailedRequests / TotalRequests * 100 : 0;
    public List<string> Errors { get; set; } = new();

    public static LoadTestResult Create(string testName)
    {
        return new LoadTestResult
        {
            TestName = testName,
            StartedAt = DateTime.UtcNow,
        };
    }
}