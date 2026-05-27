using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using SkiaSharp;

namespace OpenLMStudio.Infrastructure.Rendering;

/// <summary>
/// Renders the Pingu home scene with igloo, sink, rug, ball, and other decorative objects.
/// </summary>
public class PinguHomeSceneRenderer
{
    private readonly ILogger<PinguHomeSceneRenderer>? _logger;
    private readonly Random _random;
    private readonly PinguHomeScene _homeScene;

    /// <summary>
    /// Paint objects for rendering.
    /// </summary>
    private SKPaint _paint = new();
    private SKPaint _strokePaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
    private SKShader? _textureShader;

    public PinguHomeSceneRenderer(
        PinguHomeScene homeScene,
        ILogger<PinguHomeSceneRenderer>? logger = null,
        Random? random = null)
    {
        _homeScene = homeScene;
        _logger = logger;
        _random = random ?? new Random();
    }

    /// <summary>
    /// Set the texture shader for the home scene.
    /// </summary>
    public void SetTextureShader(SKShader shader)
    {
        _textureShader = shader;
    }

    /// <summary>
    /// Render the home scene to a SkiaSharp canvas.
    /// </summary>
    public void Render(SKCanvas canvas, float width, float height)
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
    /// Set the background color of the home scene.
    /// </summary>
    public void SetBackgroundColor(string color)
    {
        // Background color is stored in the home scene
    }

    /// <summary>
    /// Draw the background of the home scene.
    /// </summary>
    private void DrawBackground(SKCanvas canvas, float width, float height)
    {
        // Gradient background
        var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                new SKColor[]
                {
                    ParseColor(_homeScene.BackgroundColor),
                    SKColors.White,
                },
                new float[] { 0f, 1f },
                SkiaSharp.SKShaderTileMode.Clamp),
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
        var w = obj.Width * width;
        var h = obj.Height * height;

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
                _paint.Color = color;
                canvas.DrawRect(x, y, w, h, _paint);
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
    /// Parse a hex color string to SKColor.
    /// </summary>
    private static SKColor ParseColor(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            return SKColors.White;

        try
        {
            hexColor = hexColor.TrimStart('#');
            if (hexColor.Length == 6)
                hexColor = "FF" + hexColor;
            return SKColor.Parse("#" + hexColor);
        }
        catch
        {
            return SKColors.White;
        }
    }
}