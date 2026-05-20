using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.Pingu;
using OpenLMStudio.Domain.Models.QEMU;
using OpenLMStudio.Infrastructure.Services.QEMU;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Pingu automation providing action animations and drag-to-pause VM management.
/// </summary>
public class PinguAutomation : IPinguAutomation
{
    private readonly IPinguStore _pingu;
    private readonly IQEMUProcessManager _qemuManager;

    public PinguAutomation(IPinguStore pingu, IQEMUProcessManager qemuManager)
    {
        _pingu = pingu;
        _qemuManager = qemuManager;
    }

    public async Task EnterControlModeAsync()
    {
        await _pingu.UpdateMoodAsync(PinguMood.Working);
    }

    public async Task HandleDragToPauseAsync(DragEvent e)
    {
        if (e?.Target == "pingu")
        {
            foreach (var vm in _qemuManager.Instances.Where(v => v.State == VMRunState.Running))
            {
                await _qemuManager.PauseVMAsync(vm.Id).ConfigureAwait(false);
            }
            await _pingu.UpdateMoodAsync(PinguMood.Idle).ConfigureAwait(false);
        }
    }

    public async Task PerformActionAsync(string action, Element? target = null)
    {
        if (target == null)
        {
            await _pingu.UpdateMoodAsync(PinguMood.Happy).ConfigureAwait(false);
            return;
        }

        switch (action)
        {
            case "startVM":
                if (target.VmId != null)
                {
                    await _qemuManager.StartVMAsync(target.VmId).ConfigureAwait(false);
                    await _pingu.UpdateMoodAsync(PinguMood.Happy).ConfigureAwait(false);
                }
                break;
            case "stopVM":
                if (target.VmId != null)
                {
                    await _qemuManager.StopVMAsync(target.VmId).ConfigureAwait(false);
                    await _pingu.UpdateMoodAsync(PinguMood.Idle).ConfigureAwait(false);
                }
                break;
            default:
                await _pingu.UpdateMoodAsync(PinguMood.Happy).ConfigureAwait(false);
                break;
        }
    }
}