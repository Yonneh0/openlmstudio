namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents GPU hardware capabilities for model inference acceleration.
/// </summary>
public record GpuDevice
{
    /// <summary>
    /// Unique identifier for the GPU device.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Device name (e.g., "NVIDIA GeForce RTX 4090").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// GPU vendor (e.g., NVIDIA, AMD).
    /// </summary>
    public string Vendor { get; init; } = string.Empty;

    /// <summary>
    /// Total VRAM available on the GPU in bytes.
    /// </summary>
    public long TotalMemoryBytes { get; init; }

    /// <summary>
    /// Currently used VRAM in bytes.
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// Whether CUDA is available for this GPU (NVIDIA).
    /// </summary>
    public bool HasCudaSupport { get; init; }

    /// <summary>
    /// Whether ROCm is available for this GPU (AMD).
    /// </summary>
    public bool HasRocmSupport { get; init; }

    /// <summary>
    /// CUDA compute capability version.
    /// </summary>
    public string? CudaComputeCapability { get; init; }

    /// <summary>
    /// Whether this GPU is currently active for inference.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Percentage of GPU memory currently utilized (0-100).
    /// </summary>
    public float MemoryUtilizationPercent
        => TotalMemoryBytes > 0 ? (UsedMemoryBytes / (float)TotalMemoryBytes) * 100 : 0;

    /// <summary>
    /// Creates a new GPU device instance.
    /// </summary>
    public GpuDevice(int id, string name, string vendor, long totalMemoryBytes)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Vendor = vendor ?? throw new ArgumentNullException(nameof(vendor));
        TotalMemoryBytes = totalMemoryBytes;
    }

    /// <summary>
    /// Creates a default placeholder GPU device for testing.
    /// </summary>
    public static GpuDevice CreateDefaultPlaceholder()
        => new(0, "No GPU Detected", "Unknown", 0)
        {
            IsActive = true
        };
}

/// <summary>
/// Represents CPU hardware information for model inference.
/// </summary>
public record CpuInfo
{
    /// <summary>
    /// CPU model name (e.g., "Intel Core i9-13900K").
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Number of physical cores.
    /// </summary>
    public int PhysicalCoreCount { get; init; }

    /// <summary>
    /// Number of logical processors (including hyperthreading).
    /// </summary>
    public int LogicalProcessorCount { get; init; }

    /// <summary>
    /// Available RAM in bytes.
    /// </summary>
    public long AvailableMemoryBytes { get; init; }

    /// <summary>
    /// Whether AVX-512 instructions are supported.
    /// </summary>
    public bool HasAvx512Support { get; init; }

    /// <summary>
    /// Whether AVX (Advanced Vector Extensions) is supported.
    /// </summary>
    public bool HasAvxSupport { get; init; }

    /// <summary>
    /// Creates a new CPU info instance.
    /// </summary>
    public CpuInfo(string model, int physicalCoreCount, int logicalProcessorCount, long availableMemoryBytes)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        PhysicalCoreCount = physicalCoreCount;
        LogicalProcessorCount = logicalProcessorCount;
        AvailableMemoryBytes = availableMemoryBytes;
    }

    /// <summary>
    /// Creates a default placeholder CPU info for testing.
    /// </summary>
    public static CpuInfo CreateDefaultPlaceholder()
        => new("Unknown", 0, 0, 0);
}

/// <summary>
/// Represents overall system device information for inference resource allocation.
/// </summary>
public record DeviceInfo
{
    /// <summary>
    /// CPU hardware information.
    /// </summary>
    public CpuInfo Cpu { get; init; } = CpuInfo.CreateDefaultPlaceholder();

    /// <summary>
    /// List of available GPU devices.
    /// </summary>
    public ICollection<GpuDevice> Gpus { get; init; } = new List<GpuDevice>();

    /// <summary>
    /// Whether any GPU is available for acceleration.
    /// </summary>
    public bool HasGpu => Gpus.Any(g => g.TotalMemoryBytes > 0 && g.IsActive);

    /// <summary>
    /// Total VRAM across all active GPUs in bytes.
    /// </summary>
    public long TotalVramBytes => Gpus.Where(g => g.IsActive).Sum(g => g.TotalMemoryBytes);

    /// <summary>
    /// Creates a new device info instance with the specified CPUs and GPUs.
    /// </summary>
    public DeviceInfo(CpuInfo cpu, ICollection<GpuDevice> gpus)
    {
        Cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
        Gpus = gpus;
    }

    /// <summary>
    /// Creates a default placeholder device info for testing.
    /// </summary>
    public static DeviceInfo CreateDefaultPlaceholder()
        => new(CpuInfo.CreateDefaultPlaceholder(), [GpuDevice.CreateDefaultPlaceholder()]);
}