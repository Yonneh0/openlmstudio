using Avalonia;
using Avalonia.Controls;
using OpenLMStudio.Domain.Models.QEMU;
using System;

namespace OpenLMStudio.Desktop.Controls;

public partial class VMWizardStep2 : UserControl
{
    private readonly Action<VMCreationForm> _onFormChanged;

    public VMWizardStep2(Action<VMCreationForm> onFormChanged)
    {
        _onFormChanged = onFormChanged;
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeControls()
    {
        CpuSlider.ValueChanged += OnCpuChanged;
        RamSlider.ValueChanged += OnRamChanged;
        DiskSlider.ValueChanged += OnDiskChanged;
    }

    private void OnCpuChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        CpuValueText.Text = $"{(int)e.NewValue} cores";
        _onFormChanged?.Invoke(GetForm());
    }

    private void OnRamChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        RamValueText.Text = $"{(int)e.NewValue} MB";
        _onFormChanged?.Invoke(GetForm());
    }

    private void OnDiskChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        DiskValueText.Text = $"{(int)e.NewValue} GB";
        _onFormChanged?.Invoke(GetForm());
    }

    public VMCreationForm GetForm()
    {
        return new VMCreationForm
        {
            CpuCores = (int)CpuSlider.Value,
            RamMB = (int)RamSlider.Value,
            DiskSizeGB = (int)DiskSlider.Value,
        };
    }
}