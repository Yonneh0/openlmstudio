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

/// <summary>
/// Immutable record holding memory information for a single model.
/// </summary>
public record ModelMemoryEntry(
    string ModelId,
    string ModelName,
    ModelType ModelType,
    long VramBytes,
    long CpuBytes,
    DateTime LastAccessed,
    int AccessCount);

/// <summary>
/// Represents a single log entry from a llama.cpp engine binary.
/// </summary>
public record LogEntry(
    string Id,
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    EngineType Source,
    bool IsImportant = false,
    string? Category = null
);

/// <summary>
/// Form model for VM creation wizard state.
/// </summary>
public class VMCreationForm
{
    public string Name { get; set; } = "vm-1";
    public ArchitectureType Architecture { get; set; } = ArchitectureType.X86_64;
    public AcceleratorType Accelerator { get; set; } = AcceleratorType.KVM;
    public int CpuCores { get; set; } = 2;
    public int RamMB { get; set; } = 2048;
    public int DiskSizeGB { get; set; } = 20;
    public List<DiskImageConfig> DiskImages { get; set; } = new();
    public List<NetworkDeviceConfig> NetworkDevices { get; set; } = new();
}

/// <summary>
/// Result from a parallel subagent execution.
/// </summary>
public class SubagentResult
{
    /// <summary>Index of this subagent (1-5).</summary>
    public int Index { get; set; }

    /// <summary>The prompt given to this subagent.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The result produced by this subagent.</summary>
    public string? Result { get; set; }

    /// <summary>Error message if this subagent failed.</summary>
    public string? Error { get; set; }

    /// <summary>Whether this subagent succeeded.</summary>
    public bool Success => Error == null;

    /// <summary>Duration of the subagent execution in milliseconds.</summary>
    public double DurationMs { get; set; }
}

/// <summary>
/// Current status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is pending execution.</summary>
    Pending = 0,

    /// <summary>Task is currently running.</summary>
    Running = 1,

    /// <summary>Task is paused.</summary>
    Paused = 2,

    /// <summary>Task is queued and waiting for dependencies.</summary>
    Queued = 3,

    /// <summary>Task has failed.</summary>
    Failed = 4,

    /// <summary>Task has been cancelled.</summary>
    Cancelled = 5,

    /// <summary>Task has completed successfully.</summary>
    Completed = 6,
}

/// <summary>
/// Represents the current phase of a task's lifecycle.
/// </summary>
public enum TaskPhase
{
    /// <summary>Planning the approach and strategy.</summary>
    Planning = 1,

    /// <summary>Executing the planned actions.</summary>
    Acting = 2,

    /// <summary>Reviewing results and validating correctness.</summary>
    Reviewing = 3,

    /// <summary>Task is complete and finalizing.</summary>
    Completed = 4,

    /// <summary>Task has failed.</summary>
    Failed = 5,
}