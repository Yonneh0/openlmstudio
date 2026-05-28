using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using SkiaSharp;
using SKPaintStyle = SkiaSharp.SKPaintStyle;
using SKPathArcSize = SkiaSharp.SKPathArcSize;
using SKPathDirection = SkiaSharp.SKPathDirection;
using System.IO;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pre-awaken tile displayed before Pingu is awake.
/// Clicking it triggers the awakening sequence.
/// Renders a miniature home scene with igloo, sink, rug, ball, fishbowl, and nest.
/// </summary>
public partial class PinguHomeTile : UserControl, IDisposable
{
    private readonly OpenLMStudio.Application.Interfaces.IPinguStore? _pingu;
    private bool _disposed;
    private bool _isAwake;
    private readonly PinguHomeScene _homeScene;
    private SkiaSharp.SKBitmap? _cachedBitmap;

    private readonly Random _random = new();

    public PinguHomeTile()
    {
        InitializeComponent();
        _homeScene = PinguHomeScene.CreateDefault();
    }

    public PinguHomeTile(OpenLMStudio.Application.Interfaces.IPinguStore? pingu) : this()
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
        RenderHomeSceneOnly(skCanvas, width, height);

        // Encode to PNG and create Avalonia bitmap
        using var stream = new MemoryStream();
        _cachedBitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
        stream.Position = 0;
        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);

        // Set as canvas background with UniformToFill stretch
        // Note: Avalonia manages the ImageBrush source disposal automatically
        canvas.Background = new Avalonia.Media.ImageBrush(avaloniaBitmap)
        {
            Stretch = Avalonia.Media.Stretch.UniformToFill,
        };
    }

    /// <summary>
    /// Called when the tile is removed from the visual tree.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
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
        _cachedBitmap = null;
    }

    /// <summary>
    /// Render just the home scene into the canvas (no penguin).
    /// </summary>
    private void RenderHomeSceneOnly(SKCanvas canvas, float width, float height)
    {
        // Clear canvas
        canvas.Clear(SKColors.White);

        // Draw background
        DrawBackground(canvas, width, height);

        // Draw each home object
        foreach (var obj in _homeScene.Objects)
        {
            if (!obj.IsVisible) continue;
            DrawHomeObject(canvas, obj, width, height);
        }
    }

    /// <summary>
    /// Draw the background of the home scene.
    /// </summary>
    private void DrawBackground(SKCanvas canvas, float width, float height)
    {
        var bgPaint = new SKPaint
        {
            Color = ParseColor(_homeScene.BackgroundColor),
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRect(0, 0, width, height, bgPaint);
        bgPaint.Dispose();
    }

    /// <summary>
    /// Draw a single home scene object.
    /// </summary>
    private void DrawHomeObject(SKCanvas canvas, PinguHomeObject obj, float width, float height)
    {
        var x = obj.X * width;
        var y = obj.Y * height;
        var w = obj.Width;
        var h = obj.Height;

        var color = ParseColor(obj.Color);

        switch (obj.Type)
        {
            case "igloo":
                DrawIgloo(canvas, x, y, w, h, color);
                break;
            case "sink":
                DrawSink(canvas, x, y, w, h, color);
                break;
            case "rug":
                DrawRug(canvas, x, y, w, h, color);
                break;
            case "ball":
                DrawBall(canvas, x, y, w, h, color);
                break;
            case "fishbowl":
                DrawFishBowl(canvas, x, y, w, h, color);
                break;
            case "nest":
                DrawNest(canvas, x, y, w, h, color);
                break;
            default:
                var objPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRect(x, y, w, h, objPaint);
                objPaint.Dispose();
                break;
        }
    }

    private void DrawIgloo(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var path = new SKPath();
        path.MoveTo(x, y + h);
        path.ArcTo(x, y, x + w, SKPathArcSize.Small, SKPathDirection.Clockwise, 0, 0);
        path.Close();

        var iglooPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawPath(path, iglooPaint);
        iglooPaint.Dispose();
        path.Dispose();

        var doorPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x + w / 2, y + h * 0.6f, w * 0.2f, doorPaint);
        doorPaint.Dispose();
    }

    private void DrawSink(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var sinkPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawRect(x, y, w, h, sinkPaint);
        sinkPaint.Dispose();

        var faucetPaint = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawLine(x + w * 0.3f, y, x + w * 0.3f, y - h * 0.3f, faucetPaint);
        canvas.DrawLine(x + w * 0.3f, y - h * 0.3f, x + w * 0.5f, y - h * 0.3f, faucetPaint);
        faucetPaint.Dispose();
    }

    private void DrawRug(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var rugPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawOval(x, y, w, h, rugPaint);
        rugPaint.Dispose();

        var patternPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawOval(x + w * 0.1f, y + h * 0.1f, w * 0.8f, h * 0.8f, patternPaint);
        patternPaint.Dispose();
    }

    private void DrawBall(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var ballPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x + w / 2, y + h / 2, Math.Min(w, h) / 2, ballPaint);
        ballPaint.Dispose();

        var highlightPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x + w * 0.35f, y + h * 0.35f, w * 0.15f, highlightPaint);
        highlightPaint.Dispose();
    }

    private void DrawFishBowl(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var bowlPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(x + w / 2, y + h / 2, Math.Min(w, h) / 2, bowlPaint);
        bowlPaint.Dispose();

        var fishPaint = new SKPaint { Color = SKColors.Orange, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x + w * 0.5f, y + h * 0.5f, w * 0.15f, fishPaint);
        fishPaint.Dispose();
    }

    private void DrawNest(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var nestPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawOval(x, y, w, h, nestPaint);
        nestPaint.Dispose();

        var eggPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x + w * 0.3f, y + h * 0.4f, w * 0.1f, eggPaint);
        canvas.DrawCircle(x + w * 0.6f, y + h * 0.45f, w * 0.08f, eggPaint);
        eggPaint.Dispose();
    }

    private static SKColor ParseColor(string hex)
    {
        if (hex.StartsWith("#"))
        {
            var color = hex.Substring(1);
            if (color.Length == 6)
            {
                var r = byte.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return new SkiaSharp.SKColor(r, g, b);
            }
        }
        return new SkiaSharp.SKColor(255, 255, 255);
    }
}
