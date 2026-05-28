using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// A SkiaSharp-backed canvas control that renders Pingu characters.
/// Embeds directly in the Avalonia visual tree.
/// </summary>
public class PinguCanvas : Control
{
    private Func<System.Numerics.Vector2, SkiaSharp.SKBitmap, SkiaSharp.SKCanvas, Task>? _renderFunc;
    private System.Numerics.Vector2 _cursorPosition;
    private bool _cursorActive;
    private int _width;
    private int _height;
    private SkiaSharp.SKBitmap? _renderBitmap;

    public PinguCanvas()
    {
        Width = 400;
        Height = 400;
    }

    /// <summary>
    /// Initialize the canvas with a render function from PinguStore.
    /// The function receives (cursorPosition, skBitmap, skCanvas) for drawing.
    /// </summary>
    public void Initialize(Func<System.Numerics.Vector2, SkiaSharp.SKBitmap, SkiaSharp.SKCanvas, Task> renderFunc)
    {
        _renderFunc = renderFunc ?? throw new ArgumentNullException(nameof(renderFunc));
        InvalidateVisual();
    }

    /// <summary>
    /// Set the cursor position for eye tracking.
    /// </summary>
    public void SetCursorPosition(System.Numerics.Vector2 position)
    {
        _cursorPosition = position;
        _cursorActive = true;
        InvalidateVisual();
    }

    /// <summary>
    /// Clear the cursor position.
    /// </summary>
    public void ClearCursorPosition()
    {
        _cursorActive = false;
        InvalidateVisual();
    }

    /// <summary>
    /// Trigger a render update.
    /// </summary>
    public void InvalidateRender()
    {
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.GetCurrentPoint(this).Position != default)
        {
            _cursorPosition = new System.Numerics.Vector2((float)e.GetCurrentPoint(this).Position.X, (float)e.GetCurrentPoint(this).Position.Y);
            _cursorActive = true;
            InvalidateVisual();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _width = (int)e.NewSize.Width;
            _height = (int)e.NewSize.Height;
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _renderFunc = null;
        _renderBitmap?.Dispose();
        _renderBitmap = null;
    }

    public override void Render(DrawingContext context)
    {
        if (_renderFunc == null)
            return;

        if (_width <= 0 || _height <= 0)
            return;

        // Dispose the previous bitmap before creating a new one (prevents memory leak)
        var oldBitmap = _renderBitmap;
        _renderBitmap = new SKBitmap(_width, _height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var skCanvas = new SKCanvas(_renderBitmap);

        // Clear with transparent background
        skCanvas.Clear(SKColors.Transparent);

        // Call the render function, passing the bitmap and canvas for drawing
        var cursor = _cursorActive ? _cursorPosition : new System.Numerics.Vector2(200f, 200f);
        // Use GetAwaiter().GetResult() instead of .Wait() to avoid deadlocks
        _renderFunc(cursor, _renderBitmap, skCanvas).GetAwaiter().GetResult();

        // Draw the SKBitmap directly using the DrawingContext
        var rect = new Avalonia.Rect(0, 0, _width, _height);
        using var skStream = new System.IO.MemoryStream();
        _renderBitmap.Encode(skStream, SKEncodedImageFormat.Png, 90);
        skStream.Position = 0;
        var wBitmap = new Avalonia.Media.Imaging.Bitmap(skStream);
        context.DrawImage(wBitmap, rect);

        // Dispose old bitmap after rendering completes (to avoid race conditions)
        oldBitmap?.Dispose();
    }
}