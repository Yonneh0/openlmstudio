using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OpenLMStudio.Domain.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenLMStudio.Desktop.Controls;

public partial class VMWizardStep3 : UserControl
{
    private readonly Action<VMCreationForm> _onFormChanged;
    private readonly ObservableCollection<DiskImageConfig> _disks = new();

    public VMWizardStep3(Action<VMCreationForm> onFormChanged)
    {
        _onFormChanged = onFormChanged;
        InitializeComponent();
        DiskList.ItemsSource = _disks;
        AddDiskButton.Click += OnAddDiskClicked;
    }

    private async void OnAddDiskClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var fileTypes = new List<FilePickerFileType>
        {
            new FilePickerFileType("Disk Images") { Patterns = new[] { "*.qcow2", "*.raw", "*.vdi", "*.vhdx", "*.vmdk" } },
            new FilePickerFileType("All Files") { Patterns = new[] { "*" } },
        };
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Disk Image",
            AllowMultiple = false,
            FileTypeFilter = fileTypes,
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.ToString();
            var disk = new DiskImageConfig(
                Id: $"disk-{_disks.Count + 1}",
                Media: "disk",
                Format: DiskFormatType.Qcow2,
                File: path);
            _disks.Add(disk);
            UpdateDiskCount();
            _onFormChanged?.Invoke(GetForm());
        }
    }

    private void UpdateDiskCount()
    {
        DiskCountText.Text = $"{_disks.Count} disk{(_disks.Count != 1 ? "s" : "")}";
    }

    public VMCreationForm GetForm()
    {
        return new VMCreationForm
        {
            DiskImages = _disks.ToList(),
        };
    }
}