using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;
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
        foreach (var arch in archs)
            ArchCombo.Items.Add(arch);
        ArchCombo.SelectedIndex = 0;

        // Populate accelerator dropdown
        var accels = Enum.GetValues<AcceleratorType>().Select(a => a.ToString());
        foreach (var accel in accels)
            AccelCombo.Items.Add(accel);
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
            var vm = await _qemuManager.CreateVMAsync(config).ConfigureAwait(false);
            Close();
        }
        catch (Exception ex)
        {
            var dlg = new Window { Title = "Error", Width = 300, Height = 150 };
            var grid = new Grid { Margin = new Thickness(16) };
            grid.Children.Add(new TextBlock
            {
                Text = ex.Message,
                TextWrapping = TextWrapping.Wrap,
            });
            dlg.Content = grid;
            _ = dlg.ShowDialog(this);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CreateButton.Click -= OnCreateClicked;
    }
}