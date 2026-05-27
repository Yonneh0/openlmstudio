using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using PinguHomeSceneRenderer = OpenLMStudio.Infrastructure.Rendering.PinguHomeSceneRenderer;
using PinguHomeScene = OpenLMStudio.Domain.Models.PinguHomeScene;
using SkiaSharp;
using System.IO;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pre-awaken tile displayed before Pingu is awake.
/// Clicking it triggers the awakening sequence.
/// Renders a miniature home scene with igloo, sink, rug, ball, fishbowl, and nest.
/// </summary>
public partial class PinguHomeTile : UserControl, IDisposable
{
    private readonly IPinguStore? _pingu;
    private bool _disposed;
    private bool _isAwake;
    private readonly PinguHomeSceneRenderer _homeRenderer;
    private SKBitmap? _cachedBitmap;

    public PinguHomeTile()
    {
        InitializeComponent();
        _homeRenderer = new PinguHomeSceneRenderer(PinguHomeScene.CreateDefault());
    }

    public PinguHomeTile(IPinguStore? pingu) : this()
    {
        _pingu = pingu;
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
    /// Render the home scene into the Canvas.
    /// </summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            RenderHomeScene();
        }
    }

    private void RenderHomeScene()
    {
        var canvas = HomeCanvas;
        if (canvas == null) return;

        var width = (int)canvas.Bounds.Width;
        var height = (int)canvas.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        // Dispose old bitmap before replacing
        var oldBitmap = _cachedBitmap;
        _cachedBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        oldBitmap?.Dispose();

        using var skCanvas = new SKCanvas(_cachedBitmap);
        _homeRenderer.Render(skCanvas, width, height);

        // Encode to PNG and create Avalonia bitmap
        using var stream = new MemoryStream();
        _cachedBitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
        stream.Position = 0;
        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);

        // Set as canvas background with UniformToFill stretch
        canvas.Background = new Avalonia.Media.ImageBrush(avaloniaBitmap)
        {
            Stretch = Avalonia.Media.Stretch.UniformToFill,
        };
    }

    /// <summary>
    /// Disposes the tile.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cachedBitmap?.Dispose();
    }
}
