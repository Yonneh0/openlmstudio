namespace OpenLMStudio.Domain.Models.QEMU;

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