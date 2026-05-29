using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for querying device (GPU/CPU/RAM) status information.
/// </summary>
public interface IDeviceStatusService
{
    /// <summary>
    /// Total GPU memory in GB. Null if GPU info is unavailable.
    /// </summary>
    double? GpuMemory { get; }

    /// <summary>
    /// Current GPU memory usage in GB.
    /// </summary>
    double? GpuUsed { get; }

    /// <summary>
    /// Number of CPU cores.
    /// </summary>
    int CpuCores { get; }

    /// <summary>
    /// Total system RAM in GB.
    /// </summary>
    double? RamTotal { get; }

    /// <summary>
    /// Current RAM usage in GB.
    /// </summary>
    double? RamUsed { get; }

    /// <summary>
    /// Whether the device is ready for model loading.
    /// </summary>
    bool IsReady { get; }
}