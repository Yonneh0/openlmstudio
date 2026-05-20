namespace OpenLMStudio.Domain.Models.QEMU;

/// <summary>
/// Supported CPU architectures for QEMU virtual machines.
/// </summary>
public enum ArchitectureType
{
    X86_64,
    I386,
    AArch64,
    ARMv7L,
    RISC_V64,
    RISC_V32,
    AVR,
    MIPS,
    MIPS64,
    MIPSEL,
    MIPS64EL,
    PPC,
    PPC64,
    PPCemb,
    SPARC,
    SPARC64
}

/// <summary>
/// Virtual machine accelerator type.
/// </summary>
public enum AcceleratorType
{
    /// <summary>
    /// Kernel-based Virtual Machine (KVM) for near-native performance.
    /// </summary>
    KVM,

    /// <summary>
    /// QEMU's built-in Tiny Code Generator (TCG) for emulation.
    /// </summary>
    TCG
}

/// <summary>
/// Disk image format for VM storage.
/// </summary>
public enum DiskFormatType
{
    Raw,
    Qcow2,
    Qed,
    Vdi,
    Vhdx,
    Vmdk
}

/// <summary>
/// Network backend type for VM networking.
/// </summary>
public enum NetworkBackendType
{
    User,
    Tap,
    Socket,
    Vde,
    Hubport
}

/// <summary>
/// Current execution state of a VM instance.
/// </summary>
public enum VMRunState
{
    Running,
    Paused,
    Debug,
    ShuttingDown,
    Shutdown
}

/// <summary>
/// CPU topology for a virtual machine.
/// </summary>
public record CpuTopology(int? Sockets, int? Dies, int? Clusters, int Cores, int Threads);

/// <summary>
/// Configuration for a disk image attached to a VM.
/// </summary>
public record DiskImageConfig(string Id, string Media, DiskFormatType Format, string File);

/// <summary>
/// Configuration for a network device attached to a VM.
/// </summary>
public record NetworkDeviceConfig(string Id, NetworkBackendType BackendType, string? MacAddress);

/// <summary>
/// QMP (QEMU Machine Protocol) socket configuration.
/// </summary>
public record QmpSocket(string Type, string? Address, int? Port);

/// <summary>
/// A running virtual machine instance managed by QEMUProcessManager.
/// </summary>
public class VMInstance
{
    public string Id { get; init; } = "";
    public ArchitectureType Architecture { get; init; }
    public string Machine { get; init; } = "";
    public AcceleratorType Accelerator { get; init; }
    /// <summary>
    /// ProcessId of the QEMU process as a string identifier.
    /// The actual Process object is managed by QEMUProcessManager (Infrastructure layer).
    /// </summary>
    public string ProcessId { get; init; } = "";
    public QmpSocket QmpSocket { get; init; } = new("tcp", null, null);
    public QmpSocket MonSocket { get; init; } = new("unix", null, null);
    public VMRunState State { get; set; }
    public CpuTopology CpuTopology { get; init; } = new(null, null, null, 1, 1);
    public long RamBytes { get; init; }
    public List<DiskImageConfig> DiskImages { get; init; } = new();
    public List<NetworkDeviceConfig> NetworkDevices { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public VMInstance(string id, ArchitectureType architecture, string machine, AcceleratorType accelerator,
        string processId, QmpSocket qmpSocket, QmpSocket monSocket, VMRunState state,
        CpuTopology cpuTopology, long ramBytes, List<DiskImageConfig> diskImages,
        List<NetworkDeviceConfig> networkDevices, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Architecture = architecture;
        Machine = machine;
        Accelerator = accelerator;
        ProcessId = processId;
        QmpSocket = qmpSocket;
        MonSocket = monSocket;
        State = state;
        CpuTopology = cpuTopology;
        RamBytes = ramBytes;
        DiskImages = diskImages;
        NetworkDevices = networkDevices;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}

/// <summary>
/// Configuration for creating a new virtual machine.
/// </summary>
public record VMCreationConfig(
    string Id,
    ArchitectureType Architecture,
    AcceleratorType Accelerator,
    CpuTopology CpuTopology,
    long RamBytes,
    List<DiskImageConfig> DiskImages,
    List<NetworkDeviceConfig> NetworkDevices);