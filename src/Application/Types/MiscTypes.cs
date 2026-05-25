using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Types;

/// <summary>
/// Result of previewing a text file's contents.
/// </summary>
public record FilePreviewResult(
    string Content,
    int TotalLines,
    string? Language,
    string FilePath);

/// <summary>
/// Result of plugin verification.
/// </summary>
public class PluginVerificationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private PluginVerificationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static PluginVerificationResult Success() => new(true, null);
    public static PluginVerificationResult Fail(string message) => new(false, message);
}

/// <summary>
/// Contains information about a file in a HuggingFace repository.
/// Shared between Application interface and Infrastructure implementation.
/// </summary>
public class HfRepoFileInfo
{
    /// <summary>The relative path of the file within the repository.</summary>
    public required string Path { get; init; }

    /// <summary>The size of the file in bytes, if available from the API.</summary>
    public long Size { get; set; }

    /// <summary>The URL to download the LFS blob, if this is a large-file model (GGUF/safetensors).</summary>
    public string? BlobUrl { get; init; }

    /// <summary>Whether this file is stored in HuggingFace LFS (Large File Storage) and requires special handling.</summary>
    public bool IsLfsFile { get; init; }
}

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

/// <summary>
/// DTO returned by GetOrCreateBudgetAsync — contains only public state needed by callers.
/// </summary>
public class ChatBudgetStateDto
{
    /// <summary>Total maximum tokens allowed for this chat's context window.</summary>
    public long MaximumTokens { get; set; }

    /// <summary>Tokens remaining before budget is exceeded.</summary>
    public long RemainingTokens { get; set; }
}

/// <summary>
/// Visual budget indicator sent to the UI showing how much of the context token budget is used.
/// </summary>
public class ContextBudgetIndicator
{
    /// <summary>Total maximum tokens allowed for this chat's context window.</summary>
    public long MaximumTokens { get; set; }

    /// <summary>Tokens currently consumed across all context components.</summary>
    public long UsedTokens { get; set; }

    /// <summary>Tokens remaining before budget is exceeded.</summary>
    public long RemainingTokens { get; set; }

    /// <summary>Percentage of budget used (0-100).</summary>
    public float PercentageUsed { get; set; }

    /// <summary>Color zone for UI display: Green ≥80% free, Yellow 5-20% free, Red <5% free.</summary>
    public ContextBudgetColorZone ColorZone { get; set; }

    /// <summary>The current compression strategy applied to this chat's context.</summary>
    public CompressionLevel CompressionStrategy { get; set; }

    /// <summary>Tokens allocated per injection type (system prompt, compressed history, etc.).</summary>
    public Dictionary<string, long> BudgetAllocationSummary { get; set; } = new();

    /// <summary>Returns an empty indicator for when no budget exists for a chat.</summary>
    public static ContextBudgetIndicator CreateEmpty() => new()
    { MaximumTokens = 0, UsedTokens = 0, RemainingTokens = 0, PercentageUsed = 0f };
}

/// <summary>Color zones for UI display of context budget indicator.</summary>
public enum ContextBudgetColorZone
{
    /// <summary>>20% remaining — green zone</summary>
    Green,

    /// <summary>5-20% remaining — yellow/warning zone</summary>
    Yellow,

    /// <summary><5% remaining — red/critical zone</summary>
    Red
}

/// <summary>
/// ActivitySource for OpenLMStudio event tracing.
/// Provides structured telemetry for agent tool calls, context compression events, and model lifecycle events.
/// Compatible with OpenTelemetry exporters (Jaeger, Zipkin, etc.) via OTLP protocol.
/// </summary>
public static class OpenLmStudioActivitySource
{
    public const string SourceName = "OpenLMStudio";

    /// <summary>
    /// Activity source instance for tracing agent operations.
    /// </summary>
    private static readonly ActivitySource _instance = new(SourceName);

    /// <summary>
    /// Gets the current activity source instance for OpenLMStudio telemetry.
    /// Use this to create spans/activities for distributed tracing.
    /// </summary>
    public static ActivitySource Instance => _instance;

    /// <summary>
    /// Starts a new span for an agent tool call execution.
    /// Tags include: tool.name, tool.status (success/failure), tool.duration_ms (added via AddEvent).
    /// </summary>
    public static Activity? StartToolCallActivity(string toolName)
    {
        var activity = _instance.StartActivity(toolName, ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("tool.name", toolName);
            activity.SetTag("openlmstudio.component", "agent");
        }

        return activity;
    }

    /// <summary>
    /// Starts a new span for context compression events.
    /// Tags include: strategy, token_count_before, token_count_after, segments_compressed, segments_evicted.
    /// </summary>
    public static Activity? StartContextCompressionActivity(string strategy)
    {
        var activity = _instance.StartActivity($"Compress-{strategy}", ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("openlmstudio.component", "context");
            activity.SetTag("compression.strategy", strategy);
        }

        return activity;
    }

    /// <summary>
    /// Starts a new span for model lifecycle events (load/unload).
    /// Tags include: model.id, model.type, engine type, memory_bytes_allocated.
    /// </summary>
    public static Activity? StartModelLifecycleActivity(string operation, string modelId, string modelType)
    {
        var activity = _instance.StartActivity($"Model-{operation}", ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("openlmstudio.component", "model");
            activity.SetTag("model.id", modelId);
            activity.SetTag("model.type", modelType);
        }

        return activity;
    }

    /// <summary>
    /// Starts a new span for sandboxed process execution.
    /// Tags include: process.command, memory_limit_bytes.
    /// </summary>
    public static Activity? StartSandboxActivity(string commandLine)
    {
        var activity = _instance.StartActivity("Sandbox-CreateProcess", ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("openlmstudio.component", "sandbox");
            activity.SetTag("process.command_line", commandLine.Substring(0, Math.Min(commandLine.Length, 256))); // Truncate for telemetry safety
        }

        return activity;
    }

    /// <summary>
    /// Starts a new span for plugin sandbox policy enforcement.
    /// Tags include: plugin_id, sandbox_file_write, sandbox_network_access, sandbox_command_execution.
    /// </summary>
    public static Activity? StartPluginSandboxActivity(string pluginId)
    {
        var activity = _instance.StartActivity("Plugin-Sandbox", ActivityKind.Server);

        if (activity != null)
        {
            activity.SetTag("openlmstudio.component", "plugin");
            activity.SetTag("plugin.id", pluginId);
        }

        return activity;
    }
}