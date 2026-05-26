using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Sidebar panel for managing QEMU virtual machines.
/// </summary>
public partial class VMPanel : UserControl
{
    private readonly IVMStore? _vmStore;
    private readonly IQEMUProcessManager? _qemuManager;
    private readonly ILogger<VMPanel>? _logger;
    private readonly ObservableCollection<VMInstance> _vmInstances = new();

    public VMPanel(IVMStore? vmStore = null, IQEMUProcessManager? qemuManager = null, ILogger<VMPanel>? logger = null)
    {
        _vmStore = vmStore;
        _qemuManager = qemuManager;
        _logger = logger;
        InitializeComponent();
        InitializeViewModel();
    }

    private void InitializeViewModel()
    {
        if (VmList != null)
            VmList.ItemsSource = _vmInstances;

        if (_vmStore != null)
        {
            foreach (var vm in _vmStore.Instances)
                _vmInstances.Add(vm);
        }
    }

    private async void OnVmStartClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try
        {
            await _qemuManager.StartVMAsync(vm.Id).ConfigureAwait(false);
            _logger?.LogInformation("VM started: {VmId}", vm.Id);
            await RefreshVmListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start VM: {VmId}", vm.Id);
        }
    }

    private async void OnVmStopClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try
        {
            await _qemuManager.StopVMAsync(vm.Id).ConfigureAwait(false);
            _logger?.LogInformation("VM stopped: {VmId}", vm.Id);
            await RefreshVmListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop VM: {VmId}", vm.Id);
        }
    }

    private async void OnVmPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try
        {
            await _qemuManager.PauseVMAsync(vm.Id).ConfigureAwait(false);
            _logger?.LogInformation("VM paused: {VmId}", vm.Id);
            await RefreshVmListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pause VM: {VmId}", vm.Id);
        }
    }

    private async void OnVmResumeClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try
        {
            await _qemuManager.ResumeVMAsync(vm.Id).ConfigureAwait(false);
            _logger?.LogInformation("VM resumed: {VmId}", vm.Id);
            await RefreshVmListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to resume VM: {VmId}", vm.Id);
        }
    }

    private async Task RefreshVmListAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_vmStore != null)
            {
                _vmInstances.Clear();
                foreach (var vm in _vmStore.Instances)
                    _vmInstances.Add(vm);
            }
        });
    }
}
