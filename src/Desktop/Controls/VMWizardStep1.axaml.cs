using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;
using System;
using System.Linq;

namespace OpenLMStudio.Desktop.Controls;

public partial class VMWizardStep1 : UserControl
{
    private readonly IQEMUProcessManager _qemuManager;
    private readonly Action<VMCreationForm> _onFormChanged;

    public VMWizardStep1(IQEMUProcessManager qemuManager, Action<VMCreationForm> onFormChanged)
    {
        _qemuManager = qemuManager;
        _onFormChanged = onFormChanged;
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeControls()
    {
        var archs = Enum.GetValues<ArchitectureType>()
            .Where(a => a != ArchitectureType.AVR)
            .Select(a => a.ToString());
        foreach (var arch in archs)
            ArchCombo.Items.Add(arch);
        ArchCombo.SelectedIndex = 0;

        var accels = Enum.GetValues<AcceleratorType>().Select(a => a.ToString());
        foreach (var accel in accels)
            AccelCombo.Items.Add(accel);
        AccelCombo.SelectedIndex = 0;

        ArchCombo.SelectionChanged += OnArchChanged;
        AccelCombo.SelectionChanged += OnAccelChanged;
        NameInput.TextChanged += OnNameChanged;
    }

    private void OnArchChanged(object? sender, SelectionChangedEventArgs e)
    {
        _onFormChanged?.Invoke(GetForm());
    }

    private void OnAccelChanged(object? sender, SelectionChangedEventArgs e)
    {
        _onFormChanged?.Invoke(GetForm());
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e)
    {
        _onFormChanged?.Invoke(GetForm());
    }

    public VMCreationForm GetForm()
    {
        return new VMCreationForm
        {
            Name = NameInput.Text ?? "vm-1",
            Architecture = (ArchitectureType)ArchCombo.SelectedIndex,
            Accelerator = (AcceleratorType)AccelCombo.SelectedIndex,
        };
    }
}