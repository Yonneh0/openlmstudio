namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Structured logging extensions for OpenLMStudio services.
/// Provides typed log events with contextual properties.
/// </summary>
public static class StructuredLoggerExtensions
{
    private static readonly ActivitySource ActivitySource = new("OpenLMStudio");
    private static readonly DiagnosticSource DiagnosticSource = new DiagnosticListener("OpenLMStudio");

    /// <summary>
    /// Logs that a model load has started.
    /// </summary>
    public static void ModelLoadStarted(this ILogger logger, string modelId, string modelType, long fileSizeBytes)
        => logger.LogInformation("[Model] Load started: {ModelId} ({ModelType}, {FileBytes:N0} bytes)",
            modelId, modelType, fileSizeBytes);

    /// <summary>
    /// Logs that a model load has completed.
    /// </summary>
    public static void ModelLoadCompleted(this ILogger logger, string modelId, string engineType, float loadTimeMs)
        => logger.LogInformation("[Model] Load completed: {ModelId} via {EngineType} in {Time:F1}ms",
            modelId, engineType, loadTimeMs);

    /// <summary>
    /// Logs that a model load has failed.
    /// </summary>
    public static void ModelLoadFailed(this ILogger logger, string modelId, string error, float elapsedMs)
        => logger.LogError("[Model] Load failed: {ModelId} after {Time:F1}ms: {Error}",
            modelId, elapsedMs, error);

    /// <summary>
    /// Logs that a model unload has started.
    /// </summary>
    public static void ModelUnloadStarted(this ILogger logger, string modelId)
        => logger.LogInformation("[Model] Unload started: {ModelId}", modelId);

    /// <summary>
    /// Logs context compression for a chat.
    /// </summary>
    public static void ContextCompressed(this ILogger logger, long chatId, int beforeTokens, int afterTokens, double ratio)
        => logger.LogInformation("[Context] Chat {ChatId}: compressed {Before}→{After} tokens ({Ratio:P1})",
            chatId, beforeTokens, afterTokens, ratio);

    /// <summary>
    /// Logs a context budget warning for a chat.
    /// </summary>
    public static void ContextBudgetWarning(this ILogger logger, long chatId, long used, long max, long remaining)
        => logger.LogWarning("[Context] Chat {ChatId}: budget {Used}/{Max} tokens ({Remaining} remaining)",
            chatId, used, max, remaining);

    /// <summary>
    /// Logs that an agent task has started.
    /// </summary>
    public static void AgentTaskStarted(this ILogger logger, string taskId, string description)
        => logger.LogInformation("[Agent] Task {TaskId} started: {Description}",
            taskId, description);

    /// <summary>
    /// Logs that an agent task has completed.
    /// </summary>
    public static void AgentTaskCompleted(this ILogger logger, string taskId, int iterations, long durationMs)
        => logger.LogInformation("[Agent] Task {TaskId} completed: {Iterations} iterations, {Duration:F0}ms",
            taskId, iterations, durationMs);

    /// <summary>
    /// Logs an agent tool call.
    /// </summary>
    public static void AgentToolCall(this ILogger logger, string taskId, string toolName, double durationMs, bool success)
        => logger.LogDebug("[Agent] {TaskId} {ToolName}: {Duration:F1}ms {Status}",
            taskId, toolName, durationMs, success ? "OK" : "FAIL");

    /// <summary>
    /// Logs that a model download has started.
    /// </summary>
    public static void ModelDownloadStarted(this ILogger logger, string modelId, string sourceUrl, long totalBytes)
        => logger.LogInformation("[Download] Started: {ModelId} ({TotalBytes:N0} bytes from {Source})",
            modelId, totalBytes, sourceUrl);

    /// <summary>
    /// Logs that a model download has completed.
    /// </summary>
    public static void ModelDownloadCompleted(this ILogger logger, string modelId, double durationSec, long bytesDownloaded)
        => logger.LogInformation("[Download] Completed: {ModelId} ({Bytes:N0} bytes in {Duration:F1}s)",
            modelId, bytesDownloaded, durationSec);

    /// <summary>
    /// Logs a disk space warning.
    /// </summary>
    public static void DiskSpaceWarning(this ILogger logger, long availableBytes, long requiredBytes, string modelId)
        => logger.LogWarning("[Download] Disk space low for {ModelId}: {Available:N0} available, {Required:N0} required",
            modelId, availableBytes, requiredBytes);

    /// <summary>
    /// Logs that the server has started.
    /// </summary>
    public static void ServerStarted(this ILogger logger, string address, int port)
        => logger.LogInformation("[Server] Started on {Address}:{Port}", address, port);

    /// <summary>
    /// Logs that the server has stopped.
    /// </summary>
    public static void ServerStopped(this ILogger logger)
        => logger.LogInformation("[Server] Stopped");

    /// <summary>
    /// Logs a GPU memory warning.
    /// </summary>
    public static void GpuMemoryWarning(this ILogger logger, long gpuId, long usedBytes, long totalBytes)
        => logger.LogWarning("[Device] GPU {GpuId} memory: {Used:N0}/{Total:N0} ({Percent:P1})",
            gpuId, usedBytes, totalBytes, (double)usedBytes / totalBytes);
}
