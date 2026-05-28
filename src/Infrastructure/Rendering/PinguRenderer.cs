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
using SKShader = SkiaSharp.SKShader;
using SKShaderTileMode = SkiaSharp.SKShaderTileMode;
using SKBlender = SkiaSharp.SKBlender;
using SKBlendMode = SkiaSharp.SKBlendMode;
using SKMatrix = SkiaSharp.SKMatrix;
using SKPoint = System.Numerics.Vector2;
using SKPath = SkiaSharp.SKPath;

namespace OpenLMStudio.Infrastructure.Rendering;

/// <summary>
/// Renders a Pingu character using SkiaSharp on an Avalonia canvas.
/// Renders mesh data as textured triangles with bone-based skinning.
/// </summary>
public class PinguRenderer : IDisposable
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
    private bool _disposed;

    /// <summary>
    /// Get the current render bitmap for copying into Avalonia.
    /// </summary>
    public SKBitmap? RenderBitmap => _bitmap;

    /// <summary>
    /// Creates a PinguRenderer with mesh data and all required services.
    /// </summary>
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
    /// Render the scene into the provided bitmap and canvas.
    /// </summary>
    public void Render(Vector2 cursorPosition, SkiaSharp.SKBitmap bitmap, SkiaSharp.SKCanvas canvas)
    {
        if (_atlasBitmap == null)
            return;

        canvas.Clear(SkiaSharp.SKColors.Transparent);

        // Draw home scene
        DrawHomeScene(canvas);

        // Draw each penguin
        foreach (var penguin in _activePenguins)
        {
            DrawPenguin(penguin, cursorPosition, canvas, 1f / 60f);
        }
    }

    /// <summary>
    /// Dispose the renderer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _surface?.Dispose();
        _bitmap?.Dispose();
        _atlasBitmap?.Dispose();
        _canvas?.Dispose();
        _paint?.Dispose();
    }

    /// <summary>
    /// Draw the home scene.
    /// X/Y are normalized (0-1), Width/Height are in absolute pixels.
    /// </summary>
    private void DrawHomeScene(SKCanvas canvas)
    {
        // Background
        using var bgPaint = new SKPaint { IsAntialias = true, IsStroke = false };
        bgPaint.Color = ParseColor(_homeScene.BackgroundColor);
        canvas.DrawRect(new SKRect(0, 0, _surfaceWidth, _surfaceHeight), bgPaint);

        // Draw objects
        foreach (var obj in _homeScene.Objects)
        {
            var x = obj.X * _surfaceWidth;
            var y = obj.Y * _surfaceHeight;
            var w = obj.Width;
            var h = obj.Height;

            using var objPaint = new SKPaint { IsAntialias = true, IsStroke = false };
            objPaint.Color = ParseColor(obj.Color);
            canvas.DrawOval(new SKRect(x, y, x + w, y + h), objPaint);
        }
    }

    /// <summary>
    /// Draw a penguin character using mesh data.
    /// </summary>
    private void DrawPenguin(PinguNPC penguin, Vector2 cursorPosition, SKCanvas canvas, float deltaTime)
    {
        // Apply animation transforms with the actual frame delta (60fps default)
        _animation.Update(deltaTime, cursorPosition);

        // Draw mesh if available
        if (_mesh != null && _mesh.Vertices.Count > 0)
        {
            DrawMesh(canvas, penguin);
        }
        else
        {
            // Fallback to simple shapes
            DrawPenguinFallback(penguin, cursorPosition, canvas);
        }
    }

    /// <summary>
    /// Draw the penguin mesh as textured triangles with UV-based texture sampling.
    /// </summary>
    private void DrawMesh(SKCanvas canvas, PinguNPC penguin)
    {
        if (_mesh == null || _atlasBitmap == null)
            return;

        // Sort triangles by Z-order for proper depth rendering
        var sortedTriangles = _mesh.Triangles
            .OrderBy(t => t.ZOrder)
            .ToList();

        // Draw triangles with UV-based texture sampling
        foreach (var triangle in sortedTriangles)
        {
            var v0 = _mesh.Vertices[triangle.Vertex0];
            var v1 = _mesh.Vertices[triangle.Vertex1];
            var v2 = _mesh.Vertices[triangle.Vertex2];

            // Transform vertices by penguin position
            var p0 = new SKPoint(v0.X + penguin.X, v0.Y + penguin.Y);
            var p1 = new SKPoint(v1.X + penguin.X, v1.Y + penguin.Y);
            var p2 = new SKPoint(v2.X + penguin.X, v2.Y + penguin.Y);

            // Compute average UV for the triangle (better than using just the first vertex)
            var avgU = (v0.U + v1.U + v2.U) / 3f;
            var avgV = (v0.V + v1.V + v2.V) / 3f;
            var avgUV = new SKPoint(avgU / _mesh.AtlasWidth, avgV / _mesh.AtlasHeight);

            // Create the triangle path
            using var path = new SKPath();
            path.MoveTo(p0);
            path.LineTo(p1);
            path.LineTo(p2);
            path.Close();

            // Create a bitmap shader that samples from the atlas using the average UV coordinates
            // The translation positions the texture at the correct UV offset within the atlas
            var localMatrix = SKMatrix.CreateTranslation(avgUV.X * _mesh.AtlasWidth, avgUV.Y * _mesh.AtlasHeight);
            using var textureShader = SKShader.CreateBitmap(
                _atlasBitmap,
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                localMatrix);

            // Apply the texture shader
            using var triPaint = new SKPaint { Shader = textureShader, IsAntialias = true, IsStroke = false };
            canvas.DrawPath(path, triPaint);
        }
    }

    /// <summary>
    /// Draw a penguin character using simple shapes (fallback).
    /// </summary>
    private void DrawPenguinFallback(PinguNPC penguin, Vector2 cursorPosition, SKCanvas canvas)
    {
        var x = penguin.X;
        var y = penguin.Y;

        // Draw body
        using var bodyPaint = new SKPaint { IsAntialias = true, IsStroke = false };
        bodyPaint.Color = ParseColor(penguin.BodyColor);
        var bodyRect = new SKRect(x - 30, y - 60, x + 30, y + 60);
        canvas.DrawOval(bodyRect, bodyPaint);

        // Draw head
        var headX = x + (float)Math.Cos(penguin.Rotation) * 40;
        var headY = y - 60 + (float)Math.Sin(penguin.Rotation) * 40;
        using var headPaint = new SKPaint { IsAntialias = true, IsStroke = false };
        headPaint.Color = ParseColor(penguin.BodyColor);
        canvas.DrawOval(new SKRect(headX - 20, headY - 20, headX + 20, headY + 20), headPaint);

        // Draw eyes
        using var eyePaint = new SKPaint { IsAntialias = true, IsStroke = false };
        eyePaint.Color = ParseColor("#FFFFFF");
        canvas.DrawOval(new SKRect(headX - 10, headY - 5, headX - 2, headY + 5), eyePaint);
        canvas.DrawOval(new SKRect(headX + 2, headY - 5, headX + 10, headY + 5), eyePaint);

        // Draw beak
        using var beakPaint = new SKPaint { IsAntialias = true, IsStroke = false };
        beakPaint.Color = ParseColor(penguin.BeakColor);
        canvas.DrawOval(new SKRect(headX - 5, headY + 5, headX + 5, headY + 12), beakPaint);

        // Draw flippers
        using var flipperPaint = new SKPaint { IsAntialias = true, IsStroke = false };
        flipperPaint.Color = ParseColor(penguin.BodyColor);
        canvas.DrawOval(new SKRect(x - 50, y - 20, x - 35, y + 20), flipperPaint);
        canvas.DrawOval(new SKRect(x + 35, y - 20, x + 50, y + 20), flipperPaint);

        // Draw legs
        using var legPaint = new SKPaint { IsAntialias = true, IsStroke = false };
        legPaint.Color = ParseColor(penguin.BeakColor);
        canvas.DrawOval(new SKRect(x - 15, y + 50, x - 5, y + 65), legPaint);
        canvas.DrawOval(new SKRect(x + 5, y + 50, x + 15, y + 65), legPaint);

        // Draw hat if equipped
        if (penguin.Hat != null)
        {
            using var hatPaint = new SKPaint { IsAntialias = true, IsStroke = false };
            hatPaint.Color = ParseColor(penguin.Hat.Color);
            var hatY = headY - 25;
            canvas.DrawOval(new SKRect(headX - 15, hatY - 10, headX + 15, hatY + 10), hatPaint);
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