using System.Numerics;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using SKColor = SkiaSharp.SKColor;
using SKImageInfo = SkiaSharp.SKImageInfo;
using SKSurface = SkiaSharp.SKSurface;
using SKBitmap = SkiaSharp.SKBitmap;
using SKCanvas = SkiaSharp.SKCanvas;
using SKPaint = SkiaSharp.SKPaint;
using SKColors = SkiaSharp.SKColors;
using SKRect = SkiaSharp.SKRect;
using SKAlphaType = SkiaSharp.SKAlphaType;

namespace OpenLMStudio.Infrastructure.Rendering;

/// <summary>
/// Renders a Pingu character using SkiaSharp on an Avalonia canvas.
/// </summary>
public class PinguRenderer
{
    private PinguMeshData? _mesh;
    private readonly PinguBoneHierarchy _hierarchy;
    private readonly PinguAnimationSystem _animation;
    private readonly PinguNPCManager _npcManager;
    private readonly PinguHomeScene _homeScene;
    private readonly byte[] _textureAtlas;
    private readonly Random _random;
    private readonly List<PinguNPC> _activePenguins;

    private int _surfaceWidth;
    private int _surfaceHeight;
    private SKSurface? _surface;
    private SKBitmap? _bitmap;
    private SKBitmap? _atlasBitmap;
    private SKCanvas? _canvas;
    private SKPaint? _paint;

    /// <summary>
    /// Get the current render bitmap for copying into Avalonia.
    /// </summary>
    public SKBitmap? RenderBitmap => _bitmap;

    public PinguRenderer(
        PinguMeshData mesh,
        PinguBoneHierarchy hierarchy,
        PinguAnimationSystem animation,
        PinguNPCManager npcManager,
        PinguHomeScene homeScene,
        byte[] textureAtlas,
        Random? random = null)
    {
        _mesh = mesh;
        _hierarchy = hierarchy;
        _animation = animation;
        _npcManager = npcManager;
        _homeScene = homeScene;
        _textureAtlas = textureAtlas;
        _random = random ?? new Random();
        _activePenguins = new List<PinguNPC> { _npcManager.Pingu };
    }

    /// <summary>
    /// Simplified constructor without mesh data (for generated data).
    /// </summary>
    public PinguRenderer(
        PinguBoneHierarchy hierarchy,
        PinguAnimationSystem animation,
        PinguNPCManager npcManager,
        PinguHomeScene homeScene,
        byte[] textureAtlas)
    {
        _hierarchy = hierarchy;
        _animation = animation;
        _npcManager = npcManager;
        _homeScene = homeScene;
        _textureAtlas = textureAtlas;
        _mesh = new PinguMeshData();
        _random = new Random();
        _activePenguins = new List<PinguNPC> { _npcManager.Pingu };
    }

    /// <summary>
    /// Initialize the renderer with a surface.
    /// </summary>
    public void Initialize(int width, int height)
    {
        _surfaceWidth = width;
        _surfaceHeight = height;
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _canvas = _surface.Canvas;
        _paint = new SKPaint { IsAntialias = true, IsStroke = false };
        _bitmap = new SKBitmap(width, height, SkiaSharp.SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        // Decode texture atlas
        using var stream = new MemoryStream(_textureAtlas);
        _atlasBitmap = SKBitmap.Decode(stream);
    }

    /// <summary>
    /// Render the scene.
    /// </summary>
    public void Render(Vector2 cursorPosition)
    {
        if (_canvas == null || _atlasBitmap == null)
            return;

        var canvas = _canvas!;
        var paint = _paint!;
        var surface = _surface;

        canvas.Clear(SkiaSharp.SKColors.Transparent);

        // Draw home scene
        DrawHomeScene(canvas, paint);

        // Draw each penguin
        foreach (var penguin in _activePenguins)
        {
            DrawPenguin(penguin, cursorPosition, canvas, paint);
        }

        surface!.Flush();
    }

    /// <summary>
    /// Dispose the renderer.
    /// </summary>
    public void Dispose()
    {
        _surface?.Dispose();
        _bitmap?.Dispose();
        _atlasBitmap?.Dispose();
        _canvas?.Dispose();
        _paint?.Dispose();
    }

    /// <summary>
    /// Draw the home scene.
    /// </summary>
    private void DrawHomeScene(SKCanvas canvas, SKPaint paint)
    {
        // Background
        paint.Color = ParseColor(_homeScene.BackgroundColor);
        canvas.DrawRect(new SKRect(0, 0, _surfaceWidth, _surfaceHeight), paint);

        // Draw objects
        foreach (var obj in _homeScene.Objects)
        {
            var x = obj.X * _surfaceWidth;
            var y = obj.Y * _surfaceHeight;
            var w = obj.Width;
            var h = obj.Height;

            paint.Color = ParseColor(obj.Color);
            canvas.DrawOval(new SKRect(x, y, x + w, y + h), paint);
        }
    }

    /// <summary>
    /// Draw a penguin character.
    /// </summary>
    private void DrawPenguin(PinguNPC penguin, Vector2 cursorPosition, SKCanvas canvas, SKPaint paint)
    {
        var x = penguin.X;
        var y = penguin.Y;

        // Apply animation transforms
        _animation.Update(1f / 60f, cursorPosition);

        // Draw body
        paint.Color = ParseColor(penguin.BodyColor);
        var bodyRect = new SKRect(x - 30, y - 60, x + 30, y + 60);
        canvas.DrawOval(bodyRect, paint);

        // Draw head
        var headX = x + (float)Math.Cos(penguin.Rotation) * 40;
        var headY = y - 60 + (float)Math.Sin(penguin.Rotation) * 40;
        paint.Color = ParseColor(penguin.BodyColor);
        canvas.DrawOval(new SKRect(headX - 20, headY - 20, headX + 20, headY + 20), paint);

        // Draw eyes
        paint.Color = ParseColor("#FFFFFF");
        canvas.DrawOval(new SKRect(headX - 10, headY - 5, headX - 2, headY + 5), paint);
        canvas.DrawOval(new SKRect(headX + 2, headY - 5, headX + 10, headY + 5), paint);

        // Draw beak
        paint.Color = ParseColor(penguin.BeakColor);
        canvas.DrawOval(new SKRect(headX - 5, headY + 5, headX + 5, headY + 12), paint);

        // Draw flippers
        paint.Color = ParseColor(penguin.BodyColor);
        canvas.DrawOval(new SKRect(x - 50, y - 20, x - 35, y + 20), paint);
        canvas.DrawOval(new SKRect(x + 35, y - 20, x + 50, y + 20), paint);

        // Draw legs
        paint.Color = ParseColor(penguin.BeakColor);
        canvas.DrawOval(new SKRect(x - 15, y + 50, x - 5, y + 65), paint);
        canvas.DrawOval(new SKRect(x + 5, y + 50, x + 15, y + 65), paint);

        // Draw hat if equipped
        if (penguin.Hat != null)
        {
            paint.Color = ParseColor(penguin.Hat.Color);
            var hatY = headY - 25;
            canvas.DrawOval(new SKRect(headX - 15, hatY - 10, headX + 15, hatY + 10), paint);
        }
    }

    /// <summary>
    /// Parse a hex color string to SKColor.
    /// </summary>
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
