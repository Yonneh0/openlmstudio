using System.Numerics;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using SkiaSharp;
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
using SKPaintStyle = SkiaSharp.SKPaintStyle;
using SKPathArcSize = SkiaSharp.SKPathArcSize;
using SKPathDirection = SkiaSharp.SKPathDirection;

namespace OpenLMStudio.Infrastructure.Services;

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
    /// Get the current render canvas for direct drawing.
    /// </summary>
    public SKCanvas? Canvas => _canvas;

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
    /// Get the current render surface.
    /// </summary>
    public SKSurface? Surface => _surface;

    /// <summary>
    /// Get the current render canvas.
    /// </summary>
    public SKCanvas? GetCanvas() => _canvas;

    /// <summary>
    /// Get the current render bitmap.
    /// </summary>
    public SKBitmap? GetBitmap() => _bitmap;

    /// <summary>
    /// Render just the home scene into the provided canvas (no penguin).
    /// </summary>
    public void RenderHomeSceneOnly(SkiaSharp.SKCanvas canvas)
    {
        canvas.Clear(SkiaSharp.SKColors.White);
        DrawHomeScene(canvas);
    }

    /// <summary>
    /// Render the scene into the provided bitmap and canvas.
    /// Note: Animation must be updated separately via PinguAnimationSystem.Update() before calling this method.
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
            DrawPenguin(penguin, cursorPosition, canvas);
        }
    }

    /// <summary>
    /// Resize the renderer to a new size.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width == _surfaceWidth && height == _surfaceHeight)
            return;

        // Dispose old resources
        _surface?.Dispose();
        _bitmap?.Dispose();
        _canvas?.Dispose();
        _paint?.Dispose();

        // Create new resources
        _surfaceWidth = width;
        _surfaceHeight = height;
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _canvas = _surface.Canvas;
        _paint = new SKPaint { IsAntialias = true, IsStroke = false };
        _bitmap = new SKBitmap(width, height, SkiaSharp.SKImageInfo.PlatformColorType, SKAlphaType.Premul);
    }

    /// <summary>
    /// Dispose the renderer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        // Dispose canvas before surface (canvas holds references to surface)
        _canvas?.Dispose();
        _surface?.Dispose();
        _bitmap?.Dispose();
        _atlasBitmap?.Dispose();
        _paint?.Dispose();
    }

    /// <summary>
    /// Set the background color of the home scene.
    /// </summary>
    public void SetBackgroundColor(string color)
    {
        _homeScene.BackgroundColor = color;
    }

    /// <summary>
    /// Draw the home scene.
    /// X/Y are normalized (0-1), Width/Height are in absolute pixels.
    /// </summary>
    private void DrawHomeScene(SKCanvas canvas)
    {
        // Draw background (includes its own clear)
        DrawBackground(canvas);

        // Draw each home object
        foreach (var obj in _homeScene.Objects)
        {
            if (!obj.IsVisible) continue;
            DrawHomeObject(canvas, obj);
        }
    }

    /// <summary>
    /// Draw the background of the home scene.
    /// </summary>
    private void DrawBackground(SKCanvas canvas)
    {
        // Clear canvas first
        canvas.Clear(SKColors.White);

        // Solid background color
        var bgPaint = new SKPaint
        {
            Color = ParseColor(_homeScene.BackgroundColor),
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRect(0, 0, _surfaceWidth, _surfaceHeight, bgPaint);
        bgPaint.Dispose();
    }

    /// <summary>
    /// Draw a single home scene object.
    /// X/Y are normalized (0-1), Width/Height are in absolute pixels.
    /// </summary>
    private void DrawHomeObject(SKCanvas canvas, PinguHomeObject obj)
    {
        var x = obj.X * _surfaceWidth;
        var y = obj.Y * _surfaceHeight;
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
                // Generic rectangle
                if (_paint != null)
                {
                    _paint.Color = color;
                    canvas.DrawRect(x, y, w, h, _paint);
                }
                break;
        }
    }

    /// <summary>
    /// Draw an igloo.
    /// </summary>
    private void DrawIgloo(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        // Semi-circle igloo shape
        var path = new SKPath();
        path.MoveTo(x, y + h);
        path.ArcTo(x, y, x + w, SKPathArcSize.Small, SKPathDirection.Clockwise, 0, 0);
        path.Close();

        var iglooPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawPath(path, iglooPaint);
        iglooPaint.Dispose();
        path.Dispose();

        // Door opening
        var doorPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(x + w / 2, y + h * 0.6f, w * 0.2f, doorPaint);
        doorPaint.Dispose();
    }

    /// <summary>
    /// Draw a sink.
    /// </summary>
    private void DrawSink(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        // Sink base
        var sinkPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRect(x, y, w, h, sinkPaint);
        sinkPaint.Dispose();

        // Faucet
        var faucetPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };
        canvas.DrawLine(x + w * 0.3f, y, x + w * 0.3f, y - h * 0.3f, faucetPaint);
        canvas.DrawLine(x + w * 0.3f, y - h * 0.3f, x + w * 0.5f, y - h * 0.3f, faucetPaint);
        faucetPaint.Dispose();
    }

    /// <summary>
    /// Draw a rug.
    /// </summary>
    private void DrawRug(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        // Elliptical rug
        var rugPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawOval(x, y, w, h, rugPaint);
        rugPaint.Dispose();

        // Pattern
        var patternPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        };
        canvas.DrawOval(x + w * 0.1f, y + h * 0.1f, w * 0.8f, h * 0.8f, patternPaint);
        patternPaint.Dispose();
    }

    /// <summary>
    /// Draw a ball.
    /// </summary>
    private void DrawBall(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var ballPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(x + w / 2, y + h / 2, Math.Min(w, h) / 2, ballPaint);
        ballPaint.Dispose();

        // Highlight
        var highlightPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(x + w * 0.35f, y + h * 0.35f, w * 0.15f, highlightPaint);
        highlightPaint.Dispose();
    }

    /// <summary>
    /// Draw a fishbowl.
    /// </summary>
    private void DrawFishBowl(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        // Bowl
        var bowlPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawCircle(x + w / 2, y + h / 2, Math.Min(w, h) / 2, bowlPaint);
        bowlPaint.Dispose();

        // Fish
        var fishPaint = new SKPaint
        {
            Color = SKColors.Orange,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(x + w * 0.5f, y + h * 0.5f, w * 0.15f, fishPaint);
        fishPaint.Dispose();
    }

    /// <summary>
    /// Draw a nest.
    /// </summary>
    private void DrawNest(SKCanvas canvas, float x, float y, float w, float h, SKColor color)
    {
        var nestPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawOval(x, y, w, h, nestPaint);
        nestPaint.Dispose();

        // Eggs
        var eggPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(x + w * 0.3f, y + h * 0.4f, w * 0.1f, eggPaint);
        canvas.DrawCircle(x + w * 0.6f, y + h * 0.45f, w * 0.08f, eggPaint);
        eggPaint.Dispose();
    }

    /// <summary>
    /// Draw a penguin character using mesh data.
    /// Note: Animation must be updated separately via PinguAnimationSystem.Update() before calling this method.
    /// </summary>
    private void DrawPenguin(PinguNPC penguin, Vector2 cursorPosition, SKCanvas canvas)
    {
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
            // UV coordinates are already normalized (0-1 range), use directly for texture sampling
            var avgU = (v0.U + v1.U + v2.U) / 3f;
            var avgV = (v0.V + v1.V + v2.V) / 3f;
            var avgUV = new SKPoint(avgU, avgV);

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

        // Draw tool if equipped
        DrawTool(canvas, penguin, headX, headY);
    }

    /// <summary>
    /// Draw a tool held by the penguin.
    /// </summary>
    private void DrawTool(SKCanvas canvas, PinguNPC penguin, float headX, float headY)
    {
        if (penguin.Tool == null || penguin.Tool.ToolType == PinguToolType.None)
            return;

        var toolType = penguin.Tool.ToolType;
        var toolX = headX + 30; // Position to the right of the penguin
        var toolY = headY + 10;

        using var toolPaint = new SKPaint { IsAntialias = true, IsStroke = false };

        switch (toolType)
        {
            case PinguToolType.Pickaxe:
                // Draw pickaxe head
                toolPaint.Color = ParseColor("#808080");
                canvas.DrawOval(new SKRect(toolX, toolY - 5, toolX + 15, toolY + 5), toolPaint);
                // Draw handle
                toolPaint.Color = ParseColor("#8B4513");
                canvas.DrawOval(new SKRect(toolX + 15, toolY - 2, toolX + 35, toolY + 2), toolPaint);
                break;

            case PinguToolType.Sledgehammer:
                // Draw hammer head
                toolPaint.Color = ParseColor("#404040");
                canvas.DrawOval(new SKRect(toolX, toolY - 8, toolX + 20, toolY + 8), toolPaint);
                // Draw handle
                toolPaint.Color = ParseColor("#8B4513");
                canvas.DrawOval(new SKRect(toolX + 20, toolY - 3, toolX + 50, toolY + 3), toolPaint);
                break;

            case PinguToolType.PokeStick:
                // Draw stick
                toolPaint.Color = ParseColor("#8B4513");
                canvas.DrawOval(new SKRect(toolX, toolY - 2, toolX + 40, toolY + 2), toolPaint);
                break;

            case PinguToolType.Paintbrush:
                // Draw brush handle
                toolPaint.Color = ParseColor("#8B4513");
                canvas.DrawOval(new SKRect(toolX, toolY - 2, toolX + 25, toolY + 2), toolPaint);
                // Draw brush bristles
                toolPaint.Color = ParseColor("#FF0000");
                canvas.DrawOval(new SKRect(toolX + 25, toolY - 4, toolX + 35, toolY + 4), toolPaint);
                break;

            case PinguToolType.Crown:
                // Draw crown
                toolPaint.Color = ParseColor("#FFD700");
                canvas.DrawOval(new SKRect(toolX - 10, toolY - 10, toolX + 10, toolY + 5), toolPaint);
                // Crown points
                canvas.DrawOval(new SKRect(toolX - 12, toolY - 15, toolX - 7, toolY - 10), toolPaint);
                canvas.DrawOval(new SKRect(toolX - 2, toolY - 15, toolX + 3, toolY - 10), toolPaint);
                canvas.DrawOval(new SKRect(toolX + 7, toolY - 15, toolX + 12, toolY - 10), toolPaint);
                break;

            case PinguToolType.Hat:
                // Draw hat
                toolPaint.Color = ParseColor("#000000");
                canvas.DrawOval(new SKRect(toolX - 15, toolY - 15, toolX + 15, toolY + 5), toolPaint);
                break;

            case PinguToolType.ChefHat:
                // Draw chef hat
                toolPaint.Color = ParseColor("#FFFFFF");
                canvas.DrawOval(new SKRect(toolX - 10, toolY - 15, toolX + 10, toolY + 5), toolPaint);
                canvas.DrawOval(new SKRect(toolX - 15, toolY - 20, toolX + 15, toolY - 15), toolPaint);
                break;

            case PinguToolType.LabCoat:
                // Draw lab coat
                toolPaint.Color = ParseColor("#FFFFFF");
                canvas.DrawOval(new SKRect(toolX - 12, toolY - 10, toolX + 12, toolY + 20), toolPaint);
                break;
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