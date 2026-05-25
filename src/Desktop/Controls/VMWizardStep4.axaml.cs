using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenLMStudio.Domain.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenLMStudio.Desktop.Controls;

public partial class VMWizardStep4 : UserControl
{
    private readonly Action<VMCreationForm> _onFormChanged;
    private readonly ObservableCollection<NetworkDeviceConfig> _networks = new();

    public VMWizardStep4(Action<VMCreationForm> onFormChanged)
    {
        _onFormChanged = onFormChanged;
        InitializeComponent();
        NetList.ItemsSource = _networks;
        AddNetButton.Click += OnAddNetClicked;
    }

    private void OnAddNetClicked(object? sender, RoutedEventArgs e)
    {
        var net = new NetworkDeviceConfig(
            Id: $"net-{_networks.Count + 1}",
            BackendType: NetworkBackendType.User,
            MacAddress: GenerateMacAddress());
        _networks.Add(net);
        UpdateNetCount();
        _onFormChanged?.Invoke(GetForm());
    }

    private static string GenerateMacAddress()
    {
        var random = new Random();
        return string.Join(":", Enumerable.Range(0, 6).Select(_ => random.Next(256).ToString("X2")));
    }

    private void UpdateNetCount()
    {
        NetCountText.Text = $"{_networks.Count} device{(_networks.Count != 1 ? "s" : "")}";
    }

    public VMCreationForm GetForm()
    {
        return new VMCreationForm
        {
            NetworkDevices = _networks.ToList(),
        };
    }
}