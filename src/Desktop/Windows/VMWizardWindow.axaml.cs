using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;
using OpenLMStudio.Infrastructure.Services;
using OpenLMStudio.Desktop.Controls;
using System;
using System.Threading.Tasks;

namespace OpenLMStudio.Desktop.Windows;

/// <summary>
/// Multi-step VM creation wizard window.
/// </summary>
public partial class VMWizardWindow : Window
{
    private readonly IQEMUProcessManager _qemuManager;
    private readonly HardwareDetector? _hardwareDetector;
    private int _step = 0;
    private readonly VMCreationForm _form = new();
    private VMWizardStep1? _step1;
    private VMWizardStep2? _step2;
    private VMWizardStep3? _step3;
    private VMWizardStep4? _step4;
    private VMWizardStep5? _step5;

    public VMWizardWindow(IQEMUProcessManager qemuManager, HardwareDetector? hardwareDetector = null)
    {
        _qemuManager = qemuManager;
        _hardwareDetector = hardwareDetector;
        InitializeComponent();
        CreateButton.Click += OnCreateClicked;
        NextButton.Click += OnNextClicked;
        PrevButton.Click += OnPrevClicked;
        CancelButton.Click += OnCancelClicked;
        this.Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CreateButton.Click -= OnCreateClicked;
        NextButton.Click -= OnNextClicked;
        PrevButton.Click -= OnPrevClicked;
        CancelButton.Click -= OnCancelClicked;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await InitializeWizardAsync().ConfigureAwait(false);
    }

    private async Task InitializeWizardAsync()
    {
        // Load hardware info asynchronously
        var hardwareInfo = "Detecting...";
        if (_hardwareDetector != null)
        {
            try
            {
                var hw = await _hardwareDetector.DetectAsync().ConfigureAwait(false);
                hardwareInfo = $"Platform: {hw.Platform} | GPU: {hw.Gpu ?? "N/A"} | RAM: {hw.RamGB} GB";
            }
            catch
            {
                hardwareInfo = "Hardware detection unavailable";
            }
        }

        // Show step 1
        ShowStep(0);
    }

    private void ShowStep(int step)
    {
        _step = step;
        WizardContent.Content = null;

        switch (step)
        {
            case 0:
                _step1 = new VMWizardStep1(_qemuManager, UpdateForm);
                WizardContent.Content = _step1;
                break;
            case 1:
                _step2 = new VMWizardStep2(UpdateForm);
                WizardContent.Content = _step2;
                break;
            case 2:
                _step3 = new VMWizardStep3(UpdateForm);
                WizardContent.Content = _step3;
                break;
            case 3:
                _step4 = new VMWizardStep4(UpdateForm);
                WizardContent.Content = _step4;
                break;
            case 4:
                _step5 = new VMWizardStep5();
                WizardContent.Content = _step5;
                UpdateReview();
                break;
        }

        UpdateNavigationButtons();
    }

    private void UpdateForm(VMCreationForm form)
    {
        _form.Name = form.Name;
        _form.Architecture = form.Architecture;
        _form.Accelerator = form.Accelerator;
        _form.CpuCores = form.CpuCores;
        _form.RamMB = form.RamMB;
        _form.DiskSizeGB = form.DiskSizeGB;
        _form.DiskImages = form.DiskImages;
        _form.NetworkDevices = form.NetworkDevices;
    }

    private void UpdateReview()
    {
        _step5?.UpdateReview(_form);
    }

    private void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        if (_step < 4)
            ShowStep(_step + 1);
    }

    private void OnPrevClicked(object? sender, RoutedEventArgs e)
    {
        if (_step > 0)
            ShowStep(_step - 1);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnCreateClicked(object? sender, RoutedEventArgs e)
    {
        CreateButton.IsEnabled = false;

        try
        {
            var config = new VMCreationConfig(
                Id: _form.Name,
                Architecture: _form.Architecture,
                Accelerator: _form.Accelerator,
                CpuTopology: new CpuTopology(null, null, null, _form.CpuCores, 1),
                RamBytes: (long)_form.RamMB * 1024 * 1024,
                DiskImages: _form.DiskImages,
                NetworkDevices: _form.NetworkDevices);

            await _qemuManager.CreateVMAsync(config).ConfigureAwait(false);
            Close();
        }
        catch (Exception ex)
        {
            var dlg = new Window { Title = "Error", Width = 400, Height = 200 };
            var grid = new Grid { Margin = new Thickness(16) };
            var tb = new TextBlock
            {
                Text = ex.Message,
                TextWrapping = TextWrapping.Wrap,
            };
            grid.Children.Add(tb);
            dlg.Content = grid;
            _ = dlg.ShowDialog(this);
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }

    private void UpdateNavigationButtons()
    {
        PrevButton.IsVisible = _step > 0;
        NextButton.IsVisible = _step < 4;
        CreateButton.IsVisible = _step == 4;
    }
}