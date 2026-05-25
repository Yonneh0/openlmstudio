using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Metrics for CPU, memory, GPU and disk resources.
/// </summary>
public class ResourceMetrics
{
    public double Cpu { get; set; }
    public double Memory { get; set; }
    public double Gpu { get; set; }
    public double Disk { get; set; }
    public List<VmResource> VmInstances { get; set; } = new();
}

/// <summary>
/// Resource usage for a single VM instance.
/// </summary>
public class VmResource
{
    public string Id { get; set; } = "";
    public ArchitectureType Architecture { get; set; }
    public long RamBytes { get; set; }
    public VMRunState State { get; set; }
    public int CpuCores { get; set; }
}

/// <summary>
/// Interface for resource monitoring and management.
/// </summary>
public interface IResourceManager
{
    /// <summary>
    /// Monitors current CPU, memory, GPU and disk usage.
    /// </summary>
    Task<ResourceMetrics> MonitorResourcesAsync();

    /// <summary>
    /// Adjusts model settings based on resource pressure.
    /// </summary>
    Task AdjustModelSettingsAsync(ResourceMetrics metrics);

    /// <summary>
    /// Compacts prompt context if it exceeds the maximum.
    /// </summary>
    Task CompactPromptIfNeededAsync(int contextLength);
}