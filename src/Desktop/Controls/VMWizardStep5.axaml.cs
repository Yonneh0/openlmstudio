using Avalonia.Controls;
using OpenLMStudio.Domain.Models;
using System;
using System.Text;

namespace OpenLMStudio.Desktop.Controls;

public partial class VMWizardStep5 : UserControl
{
    public VMWizardStep5()
    {
        InitializeComponent();
    }

    public void UpdateReview(VMCreationForm form)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"VM Name: {form.Name}");
        sb.AppendLine($"Architecture: {form.Architecture}");
        sb.AppendLine($"Accelerator: {form.Accelerator}");
        sb.AppendLine($"CPU Cores: {form.CpuCores}");
        sb.AppendLine($"RAM: {form.RamMB} MB");
        sb.AppendLine($"Disk Size: {form.DiskSizeGB} GB");
        sb.AppendLine();

        if (form.DiskImages?.Count > 0)
        {
            sb.AppendLine("Disk Images:");
            foreach (var disk in form.DiskImages)
                sb.AppendLine($"  - {disk.File} ({disk.Format})");
            sb.AppendLine();
        }

        if (form.NetworkDevices?.Count > 0)
        {
            sb.AppendLine("Network Devices:");
            foreach (var net in form.NetworkDevices)
                sb.AppendLine($"  - {net.Id}: {net.BackendType} (MAC: {net.MacAddress})");
            sb.AppendLine();
        }

        ReviewText.Text = sb.ToString();
    }
}