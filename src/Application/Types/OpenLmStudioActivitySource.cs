using System.Diagnostics;

namespace OpenLMStudio.Application.Types;

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