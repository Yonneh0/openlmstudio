using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;

namespace OpenLMStudio.Desktop.Windows;

/// <summary>
/// VM creation wizard window.
/// </summary>
public partial class VMWizardWindow : Window
{
    private readonly IQEMUProcessManager _qemuManager;

    public VMWizardWindow(IQEMUProcessManager qemuManager)
    {
        _qemuManager = qemuManager;
        InitializeComponent();
        InitializeControls();

        CreateButton.Click += OnCreateClicked;
        this.Closed += OnClosed;
    }

    private void InitializeControls()
    {
        // Populate architecture dropdown
        var archs = Enum.GetValues<ArchitectureType>()
            .Where(a => a != Domain.Models.QEMU.ArchitectureType.AVR)
            .Select(a => a.ToString());
        ArchCombo.Items = archs;
        ArchCombo.SelectedIndex = 0;

        // Populate accelerator dropdown
        var accels = Enum.GetValues<AcceleratorType>().Select(a => a.ToString());
        AccelCombo.Items = accels;
        AccelCombo.SelectedIndex = 0;

        // Set defaults
        RamSlider.Value = 2048;
        CpuSlider.Value = 2;
    }

    private async void OnCreateClicked(object? sender, RoutedEventArgs e)
    {
        var arch = (ArchitectureType)ArchCombo.SelectedIndex;
        var accel = (AcceleratorType)AccelCombo.SelectedIndex;
        var name = NameInput.Text ?? "vm-1";

        var config = new VMCreationConfig(
            Id: name,
            Architecture: arch,
            Accelerator: accel,
            CpuTopology: new CpuTopology(null, null, null, (int)CpuSlider.Value, 1),
            RamBytes: (long)RamSlider.Value * 1024 * 1024,
            DiskImages: new List<DiskImageConfig>(),
            NetworkDevices: new List<NetworkDeviceConfig>());

        try
        {
            var vm = await _qemuManager.CreateVMAsync(config);
            Close();
        }
        catch (Exception ex)
        {
            var dlg = new Window { Title = "Error", Width = 300, Height = 150 };
            var tb = new TextBlock
            {
                Text = ex.Message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
            };
            dlg.Content = tb;
            dlg.ShowDialog(this);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CreateButton.Click -= OnCreateClicked;
    }
}