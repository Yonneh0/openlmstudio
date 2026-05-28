using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using IPinguStore = OpenLMStudio.Application.Interfaces.IPinguStore;
using OpenLMStudio.Infrastructure;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pingu overlay panel with tabs for Skills, Settings, Models, Compile, Logs, About.
/// </summary>
public partial class PinguPanel : UserControl, IDisposable
{
    private IPinguStore? _pingu;
    private bool _disposed;

    public PinguPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the Pingu store after construction.
    /// </summary>
    public void SetPinguStore(IPinguStore pingu)
    {
        // Unsubscribe from old store
        if (_pingu != null)
        {
            _pingu.OnStateChanged -= OnPinguStateChanged;
        }

        _pingu = pingu ?? throw new ArgumentNullException(nameof(pingu));

        // Subscribe to state changes
        _pingu.OnStateChanged += OnPinguStateChanged;
    }

    private void OnPinguStateChanged(object? sender, PinguStateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (PanelTabs == null) return;

            var tabIndex = e.State.ActivePanel switch
            {
                PinguPanelType.Skills => 0,
                PinguPanelType.Settings => 1,
                PinguPanelType.Models => 2,
                PinguPanelType.Compile => 3,
                PinguPanelType.Logs => 4,
                PinguPanelType.About => 5,
                _ => 0
            };
            if (PanelTabs.SelectedIndex != tabIndex)
                PanelTabs.SelectedIndex = tabIndex;

            // Update slider values when state changes
            UpdateSliders(e.State);
        });
    }

    private void UpdateSliders(PinguState state)
    {
        // Update animation speed slider
        if (AnimationSpeedSlider != null)
        {
            AnimationSpeedSlider.Value = Math.Max(AnimationSpeedSlider.Minimum, Math.Min(AnimationSpeedSlider.Maximum, state.AnimationSpeedMultiplier));
        }

        // Update behavior frequency slider
        if (BehaviorFrequencySlider != null)
        {
            BehaviorFrequencySlider.Value = Math.Max(BehaviorFrequencySlider.Minimum, Math.Min(BehaviorFrequencySlider.Maximum, state.BehaviorFrequencyMultiplier));
        }

        // Update pingu size slider
        if (PinguSizeSlider != null)
        {
            PinguSizeSlider.Value = Math.Max(PinguSizeSlider.Minimum, Math.Min(PinguSizeSlider.Maximum, state.PinguSizeMultiplier));
        }
    }

    private void OnAnimationSpeedSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_pingu != null)
        {
            _pingu.AnimationSpeedMultiplier = (float)e.NewValue;
        }
    }

    private void OnBehaviorFrequencySliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_pingu != null)
        {
            _pingu.BehaviorFrequencyMultiplier = (float)e.NewValue;
        }
    }

    private void OnPinguSizeSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_pingu != null)
        {
            _pingu.PinguSizeMultiplier = (float)e.NewValue;
        }
    }

    /// <summary>
    /// Disposes the panel and unregisters from Pingu state changes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_pingu != null)
        {
            _pingu.OnStateChanged -= OnPinguStateChanged;
        }
    }
}