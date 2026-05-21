using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Reactive store for managed VM instances.
/// </summary>
public class VMStore : IVMStore, IDisposable
{
    private readonly ConcurrentDictionary<string, VMInstance> _instances = new();
    private Timer? _stateTimer;
    private object _stateLock = new();

    public IEnumerable<VMInstance> Instances => _instances.Values;
    public event EventHandler? OnStateChanged;

    public VMStore()
    {
        _stateTimer = new Timer(OnStateTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        // Start the timer so state changes are periodically notified
        _stateTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    private void OnStateTimerTick(object? state)
    {
        NotifyStateChanged();
        _stateTimer!.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddAsync(VMInstance vm)
    {
        _instances.AddOrUpdate(vm.Id, vm, (k, v) => vm);
        NotifyStateChanged();
    }

    public async Task RemoveAsync(string vmId)
    {
        _instances.TryRemove(vmId, out _);
        NotifyStateChanged();
    }

    public async Task<VMInstance?> GetAsync(string vmId)
    {
        _instances.TryGetValue(vmId, out var vm);
        return vm;
    }

    public async Task UpdateAsync(VMInstance vm)
    {
        _instances.AddOrUpdate(vm.Id, vm, (k, v) => vm);
        NotifyStateChanged();
    }

    public void Dispose()
    {
        _stateTimer?.Dispose();
    }
}