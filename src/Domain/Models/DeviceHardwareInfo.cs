namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents information about a GPU device in the system.
/// </summary>
public record DeviceHardwareInfo(
    string Name,
    string Vendor,
    long TotalMemoryBytes,
    long FreeMemoryBytes,
    int ComputeCapability,
    bool IsCudaCompatible
);