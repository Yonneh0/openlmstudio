using SkiaSharp;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Renders the Pingu home scene (background + static objects) with Z-order depth sorting.
/// </summary>
public static class PinguHomeSceneRenderer
{
    /// <summary>
    /// Draw the home scene onto a canvas with proper Z-order sorting.
    /// Objects are sorted by their Z coordinate for depth-based rendering order.
    /// </summary>
    public static void DrawHomeScene(SKCanvas canvas, PinguHomeScene scene, int width, int height)
    {
        // Draw background
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            IsStroke = false,
            Color = ParseColor(scene.BackgroundColor)
        };
        canvas.DrawRect(0, 0, width, height, bgPaint);

        // Sort objects by Z for depth ordering (lower Z = further back, drawn first)
        var sorted = scene.SortedObjects.ToList();
        foreach (var obj in sorted)
        {
            if (!obj.IsVisible)
                continue;

            var x = (float)(obj.X * width);
            var y = (float)(obj.Y * height);
            var w = obj.Width;
            var h = obj.Height;

            using var objPaint = new SKPaint
            {
                IsAntialias = true,
                IsStroke = false,
                Color = ParseColor(obj.Color),
                PathEffect = obj.IsInteractive ? SKPathEffect.CreateDiscrete(w * 0.02f, 3) : null
            };

            // Draw the object as a rounded rectangle (consistent with PinguRenderer)
            var rect = new SKRect(x, y, x + w, y + h);
            canvas.DrawOval(rect, objPaint);
        }
    }

    private static SKColor ParseColor(string color)
    {
        if (color.StartsWith("#") && color.Length == 7)
        {
            return new SKColor(
                Convert.ToByte(color.Substring(1, 2), 16),
                Convert.ToByte(color.Substring(3, 2), 16),
                Convert.ToByte(color.Substring(5, 2), 16));
        }

        return SKColors.Gray;
    }
}