using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pre-awaken tile displayed before Pingu is awake.
/// Clicking it triggers the awakening sequence.
/// </summary>
public partial class PinguHomeTile : UserControl, IDisposable
{
    private readonly IPinguStore? _pingu;
    private bool _disposed;
    private bool _isAwake;

    public PinguHomeTile(IPinguStore? pingu = null)
    {
        _pingu = pingu;
        InitializeComponent();
    }

    /// <summary>
    /// Called when the tile is clicked to awaken Pingu.
    /// </summary>
    private async void OnTileClicked(object? sender, PointerPressedEventArgs e)
    {
        if (_isAwake) return;

        _isAwake = true;

        // Trigger awakening via Pingu store
        if (_pingu != null)
        {
            try
            {
                await _pingu.StartAwakeningSequenceAsync();
            }
            catch
            {
                // If awakening fails, just show the panel
                _isAwake = false;
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Disposes the tile.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}