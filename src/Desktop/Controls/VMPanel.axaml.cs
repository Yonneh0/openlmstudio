using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private readonly ObservableCollection<VMInstance> _vmInstances = new();

    public VMPanel(IVMStore? vmStore = null, IQEMUProcessManager? qemuManager = null)
    {
        _vmStore = vmStore;
        _qemuManager = qemuManager;
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
        try { await _qemuManager.StartVMAsync(vm.Id).ConfigureAwait(false); }
        catch { }
    }

    private async void OnVmStopClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try { await _qemuManager.StopVMAsync(vm.Id).ConfigureAwait(false); }
        catch { }
    }

    private async void OnVmPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try { await _qemuManager.PauseVMAsync(vm.Id).ConfigureAwait(false); }
        catch { }
    }

    private async void OnVmResumeClicked(object? sender, RoutedEventArgs e)
    {
        if (_qemuManager == null || VmList?.SelectedItem is not VMInstance vm) return;
        try { await _qemuManager.ResumeVMAsync(vm.Id).ConfigureAwait(false); }
        catch { }
    }
}
