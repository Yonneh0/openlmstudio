using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Manages QEMU virtual machine instances, including creation, lifecycle, and QMP protocol.
/// </summary>
public interface IQEMUProcessManager : IDisposable
{
    /// <summary>
    /// Collection of currently managed VM instances.
    /// </summary>
    IEnumerable<VMInstance> Instances { get; }

    /// <summary>
    /// Creates a new virtual machine with the given configuration.
    /// </summary>
    Task<VMInstance> CreateVMAsync(VMCreationConfig config);

    /// <summary>
    /// Starts a paused VM instance.
    /// </summary>
    Task StartVMAsync(string vmId);

    /// <summary>
    /// Pauses a running VM instance.
    /// </summary>
    Task PauseVMAsync(string vmId);

    /// <summary>
    /// Resumes a paused VM instance.
    /// </summary>
    Task ResumeVMAsync(string vmId);

    /// <summary>
    /// Gracefully stops a VM instance.
    /// </summary>
    Task StopVMAsync(string vmId);

    /// <summary>
    /// Forcefully deletes a VM and cleans up resources.
    /// </summary>
    Task DeleteVMAsync(string vmId);

    /// <summary>
    /// Executes a QMP command on a VM's management socket.
    /// </summary>
    Task<object?> ExecuteQMPCommandAsync(string vmId, string command, Dictionary<string, object?>? args = null);

    /// <summary>
    /// Queries the block devices of a VM.
    /// </summary>
    Task<List<object>> QueryBlockDevicesAsync(string vmId);
}