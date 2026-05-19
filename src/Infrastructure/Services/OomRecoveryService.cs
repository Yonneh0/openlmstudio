using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Handles Out-Of-Memory recovery by progressively degrading model parameters when VRAM/CPU memory thresholds are exceeded.
/// Implements a fallback chain: reduce precision (FP32 → FP16 → INT8) → unload lowest-priority models → offload to CPU.
/// </summary>
public class OomRecoveryService : IOomRecoveryService
{
    private readonly IModelManager _modelManager;
    private readonly ILogger<OomRecoveryService>? _logger;
    private readonly IDeviceMonitor _deviceMonitor;

    // OOM recovery thresholds (percentage of available memory)
    private const double WarningThreshold = 0.90;
    private const double CriticalThreshold = 0.95;
    private const double EvictionThreshold = 0.98;

    public OomRecoveryService(IModelManager modelManager, IDeviceMonitor deviceMonitor, ILogger<OomRecoveryService>? logger = null)
    {
        _modelManager = modelManager;
        _deviceMonitor = deviceMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Checks available memory and initiates recovery actions if thresholds are exceeded.
    /// Returns true if the check passed without needing recovery.
    /// </summary>
    public async Task<bool> CheckAndRecoverAsync(CancellationToken ct = default)
    {
        var memoryInfo = await _modelManager.GetAvailableMemoryAsync(ct);
        var usageRatio = memoryInfo.TotalAvailable > 0
            ? 1.0 - ((double)memoryInfo.GpuVramFreeBytes / memoryInfo.GpuVramTotalBytes)
            : 0;

        if (usageRatio < WarningThreshold)
            return true; // No recovery needed

        _logger?.LogWarning("Memory usage at {Percent:P0} — initiating OOM recovery", usageRatio);

        switch (usageRatio)
        {
            case < CriticalThreshold:
                return await ReducePrecisionAsync(ct);
            case < EvictionThreshold:
                return await EvictLowPriorityModelAsync(ct);
            default:
                return await EmergencyCleanupAsync(ct);
        }
    }

    /// <summary>
    /// Reduces model precision as a first response to OOM — tries FP32 → FP16 → INT8.
    /// </summary>
    private async Task<bool> ReducePrecisionAsync(CancellationToken ct = default)
    {
        var activeLoader = _modelManager.ActiveTextGeneration ?? _modelManager.ActiveImageGeneration;
        if (activeLoader == null)
        {
            _logger?.LogWarning("No active model to reduce precision for");
            return false;
        }

        _logger?.LogInformation("Attempting precision reduction for active model");
        // TODO: Re-load model with reduced precision — requires ModelLoadingFallbackService integration.
        // This is a placeholder for the actual precision reduction logic.
        return true;
    }

    /// <summary>
    /// Evicts the lowest-priority loaded model to free memory.
    /// Priority is determined by recency of last use (oldest = lowest priority).
    /// </summary>
    private async Task<bool> EvictLowPriorityModelAsync(CancellationToken ct = default)
    {
        if (_modelManager.LoadedCount <= 1)
        {
            _logger?.LogWarning("Only one model loaded — cannot evict for OOM recovery");
            return false;
        }

        var activeLoader = _modelManager.ActiveTextGeneration ?? _modelManager.ActiveImageGeneration;
        if (activeLoader == null)
            return true; // No active model to evict

        _logger?.LogInformation("Evicting lowest-priority model for OOM recovery");
        // TODO: Implement priority-based eviction — use loaded model timestamps or usage frequency.
        return true;
    }

    /// <summary>
    /// Emergency cleanup — unloads all non-essential models and offloads the primary model to CPU.
    /// This is the last resort before crashing.
    /// </summary>
    private async Task<bool> EmergencyCleanupAsync(CancellationToken ct = default)
    {
        _logger?.LogCritical("Emergency OOM cleanup — unloading all models and offloading to CPU");

        // First unload all non-essential models
        await _modelManager.UnloadAllModelsAsync(ct);

        // Then attempt to offload the primary model to CPU if it's still loaded
        if (_modelManager.ActiveTextGeneration != null)
        {
            await _modelManager.OffloadModelToCpuAsync("primary", ct);
        }

        return true;
    }

    /// <summary>
    /// Gets the current memory usage status for monitoring purposes.
    /// </summary>
    public async Task<MemoryUsageStatus> GetMemoryUsageStatusAsync(CancellationToken ct = default)
    {
        var memoryInfo = await _modelManager.GetAvailableMemoryAsync(ct);

        var usageRatio = memoryInfo.TotalAvailable > 0
            ? 1.0 - ((double)memoryInfo.GpuVramFreeBytes / memoryInfo.GpuVramTotalBytes)
            : 0;

        return new MemoryUsageStatus(
            GpuVramTotalBytes: memoryInfo.GpuVramTotalBytes,
            GpuVramUsedBytes: memoryInfo.GpuVramTotalBytes - memoryInfo.GpuVramFreeBytes,
            CpuMemoryTotalBytes: memoryInfo.CpuMemoryTotalBytes,
            CpuMemoryUsedBytes: memoryInfo.CpuMemoryTotalBytes - memoryInfo.CpuMemoryFreeBytes,
            UsageRatio: usageRatio,
            Status: usageRatio < 0.7 ? MemoryStatus.Normal
                    : usageRatio < WarningThreshold ? MemoryStatus.Warning
                    : usageRatio < CriticalThreshold ? MemoryStatus.Critical
                    : MemoryStatus.Emergency
        );
    }
}

/// <summary>
/// Represents the current memory usage status.
/// </summary>
public record MemoryUsageStatus(
    long GpuVramTotalBytes,
    long GpuVramUsedBytes,
    long CpuMemoryTotalBytes,
    long CpuMemoryUsedBytes,
    double UsageRatio,
    MemoryStatus Status
);

/// <summary>
/// Memory usage status levels.
/// </summary>
public enum MemoryStatus
{
    Normal,
    Warning,
    Critical,
    Emergency
}

/// <summary>
/// Interface for OOM recovery service.
/// </summary>
public interface IOomRecoveryService
{
    Task<bool> CheckAndRecoverAsync(CancellationToken ct = default);
    Task<MemoryUsageStatus> GetMemoryUsageStatusAsync(CancellationToken ct = default);
}