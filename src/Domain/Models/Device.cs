namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents a hardware device that can perform inference (GPU, CPU).
/// </summary>
public record Device(
    Guid Id,
    string Name,
    string Type,              // "GPU", "CPU"
    string Vendor,            // e.g., "NVIDIA", "AMD", "Intel"
    long MemoryBytes,         // Total available memory
    long UsedMemoryBytes,     // Currently allocated memory
    bool IsAvailable,
    float UtilizationPercent, // 0-100% utilization
    IReadOnlyDictionary<string, string> Capabilities)
{
    /// <summary>
    /// Creates a GPU device instance.
    /// </summary>
    public static Device CreateGpu(string name, string vendor, long memoryBytes)
        => new(
            Guid.NewGuid(),
            name,
            "GPU",
            vendor,
            memoryBytes,
            0,
            true,
            0,
            new Dictionary<string, string> { { "type", "gpu" } });

    /// <summary>
    /// Creates a CPU device instance.
    /// </summary>
    public static Device CreateCpu(string name, int coreCount)
        => new(
            Guid.NewGuid(),
            name,
            "CPU",
            "",
            0,
            0,
            true,
            0,
            new Dictionary<string, string> { { "cores", coreCount.ToString() } });

    /// <summary>
    /// Available memory for model loading. For GPUs, this is the free VRAM (TotalVRAM - UsedVRAM).
    /// For CPUs, this returns the total system RAM tracked by DeviceMonitor — updated externally via SetAvailableMemoryOverride().
    /// </summary>
    public long AvailableMemory => Type == "GPU" ? MemoryBytes - UsedMemoryBytes : _availableMemoryOverride ?? MemoryBytes;

    private long? _availableMemoryOverride;

    /// <summary>
    /// Overrides the available memory value for CPU devices with actual system RAM data from DeviceMonitor.
    /// For GPU devices, AvailableMemory is always computed as TotalVRAM - UsedVRAM and this override has no effect.
    /// </summary>
    public void SetAvailableMemoryOverride(long bytes) => _availableMemoryOverride = bytes;
}