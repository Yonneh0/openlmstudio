using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Pingu automation for actions like drag-to-pause, action animations.
/// Minimal stub — full implementation requires UI integration.
/// </summary>
public class PinguAutomation : IDisposable
{
    private readonly ILogger<PinguAutomation> _logger;
    private readonly IQEMUProcessManager? _qemuManager;

    public PinguAutomation(ILogger<PinguAutomation> logger, IQEMUProcessManager? qemuManager = null)
    {
        _logger = logger;
        _qemuManager = qemuManager;
    }

    /// <summary>
    /// Pauses all running VMs when user drags Pingu to a corner.
    /// </summary>
    public async Task HandleDragToPauseAsync()
    {
        if (_qemuManager == null) return;

        foreach (var vm in _qemuManager.Instances.Where(v => v.State == Domain.Models.QEMU.VMRunState.Running))
        {
            try
            {
                await _qemuManager.PauseVMAsync(vm.Id).ConfigureAwait(false);
                _logger.LogInformation("Paused VM {VmId} via Pingu drag-to-pause", vm.Id);
            }
            catch { }
        }
    }

    public void Dispose() { }
}