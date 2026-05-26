using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
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
        });
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