using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Rendering;
using OpenLMStudio.Infrastructure.Services;
using SkiaSharp;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pingu character view that renders the full character with animation, IK, and physics.
/// Uses SkiaSharp directly via Avalonia's DrawingContext.
/// </summary>
public partial class PinguCharacterView : Control
{
    private readonly ILogger<PinguCharacterView>? _logger;
    private readonly PinguAnimationSystem _animationSystem;
    private readonly PinguAnimationStateMachine _stateMachine;
    private readonly PinguBehaviorTriggers _behaviorTriggers;
    private readonly PinguPhysicsSolver _physicsSolver;
    private readonly PinguInverseKinematics _ik;
    private readonly PinguToolHolder _toolHolder;
    private readonly PinguHomeSceneRenderer _homeSceneRenderer;
    private readonly Random _random;

    private float _surfaceWidth;
    private float _surfaceHeight;
    private Vector2 _cursorPosition;
    private bool _isPointerDown;
    private bool _isInitialized;

    // Default constructor for XAML
    public PinguCharacterView()
    {
    }

    public PinguCharacterView(
        PinguAnimationSystem animationSystem,
        PinguAnimationStateMachine stateMachine,
        PinguBehaviorTriggers behaviorTriggers,
        PinguPhysicsSolver physicsSolver,
        PinguInverseKinematics ik,
        PinguToolHolder toolHolder,
        PinguHomeSceneRenderer homeSceneRenderer,
        ILogger<PinguCharacterView>? logger = null,
        Random? random = null)
    {
        _animationSystem = animationSystem;
        _stateMachine = stateMachine;
        _behaviorTriggers = behaviorTriggers;
        _physicsSolver = physicsSolver;
        _ik = ik;
        _toolHolder = toolHolder;
        _homeSceneRenderer = homeSceneRenderer;
        _logger = logger;
        _random = random ?? new Random();
        _isInitialized = true;
    }

    /// <summary>
    /// Update the character for one frame. Call from render loop.
    /// </summary>
    public void Update(float deltaTime)
    {
        _stateMachine.Update(deltaTime);
        _behaviorTriggers.Update(deltaTime);
        _toolHolder.Update(deltaTime);

        // Update IK
        _ik.UpdatePositions(new PinguBoneHierarchy());
        _ik.Solve();

        // Update physics
        var positions = new List<Vector3>();
        for (var i = 0; i < _ik.BoneRotations.Length / 3; i++)
        {
            positions.Add(new Vector3(_ik.BonePositions[i * 3], _ik.BonePositions[i * 3 + 1], _ik.BonePositions[i * 3 + 2]));
        }
        _physicsSolver.Solve(positions, deltaTime);
    }

    /// <summary>
    /// Render the character to the canvas.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        if (_surfaceWidth <= 0 || _surfaceHeight <= 0) return;

        canvas.Clear(SKColors.Transparent);
        _homeSceneRenderer.Render(canvas, _surfaceWidth, _surfaceHeight);

        // Draw each penguin
        var positions = new List<Vector3>();
        for (var i = 0; i < _ik.BoneRotations.Length / 3; i++)
        {
            positions.Add(new Vector3(_ik.BonePositions[i * 3], _ik.BonePositions[i * 3 + 1], _ik.BonePositions[i * 3 + 2]));
        }

        DrawPenguin(canvas, positions);
    }

    /// <summary>
    /// Draw a single penguin character.
    /// </summary>
    private void DrawPenguin(SKCanvas canvas, IReadOnlyList<Vector3> positions)
    {
        var boneCount = _ik.BoneRotations.Length / 3;
        if (boneCount == 0) return;

        // Body (ellipse)
        var bodyCenter = positions[0];
        var bodyRadius = Math.Max(10, boneCount * 2);
        var bodyPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawCircle((int)bodyCenter.X, (int)bodyCenter.Y, bodyRadius, bodyPaint);
        bodyPaint.Dispose();

        // Head
        var headIndex = boneCount > 2 ? 1 : 0;
        var headPos = positions[headIndex];
        var headRadius = bodyRadius * 0.7f;
        var headPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawCircle((int)headPos.X, (int)headPos.Y, headRadius, headPaint);
        headPaint.Dispose();

        // Eyes
        var eyePaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle((int)(headPos.X - headRadius * 0.3f), (int)(headPos.Y - headRadius * 0.2f), 3, eyePaint);
        canvas.DrawCircle((int)(headPos.X + headRadius * 0.3f), (int)(headPos.Y - headRadius * 0.2f), 3, eyePaint);
        eyePaint.Dispose();

        // Beak
        var beakPaint = new SKPaint
        {
            Color = SKColors.Orange,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle((int)headPos.X, (int)(headPos.Y + headRadius * 0.3f), 5, beakPaint);
        beakPaint.Dispose();

        // Flippers
        var flipperPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle((int)(bodyCenter.X - bodyRadius), (int)(bodyCenter.Y + bodyRadius * 0.5f), bodyRadius * 0.3f, flipperPaint);
        canvas.DrawCircle((int)(bodyCenter.X + bodyRadius), (int)(bodyCenter.Y + bodyRadius * 0.5f), bodyRadius * 0.3f, flipperPaint);
        flipperPaint.Dispose();

        // Legs
        var legPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle((int)(bodyCenter.X - bodyRadius * 0.4f), (int)(bodyCenter.Y + bodyRadius), 6, legPaint);
        canvas.DrawCircle((int)(bodyCenter.X + bodyRadius * 0.4f), (int)(bodyCenter.Y + bodyRadius), 6, legPaint);
        legPaint.Dispose();

        // Draw tools
        for (var i = 0; i < _toolHolder.Attachments.Count; i++)
        {
            var attachment = _toolHolder.Attachments[i];
            var toolPos = _toolHolder.GetToolPosition(i, positions);
            var toolPaint = new SKPaint
            {
                Color = SKColors.Brown,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawCircle((int)toolPos.X, (int)toolPos.Y, 8, toolPaint);
            toolPaint.Dispose();
        }
    }

    /// <summary>
    /// Override Render to draw with SkiaSharp on the DrawingContext.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (!_isInitialized)
            return;

        if (_surfaceWidth <= 0 || _surfaceHeight <= 0)
            return;

        using var skBitmap = new SkiaSharp.SKBitmap((int)_surfaceWidth, (int)_surfaceHeight, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
        using var skCanvas = new SkiaSharp.SKCanvas(skBitmap);

        skCanvas.Clear(SkiaSharp.SKColors.Transparent);
        Render(skCanvas);

        var rect = new Avalonia.Rect(0, 0, _surfaceWidth, _surfaceHeight);
        using var skStream = new System.IO.MemoryStream();
        skBitmap.Encode(skStream, SkiaSharp.SKEncodedImageFormat.Png, 90);
        skStream.Position = 0;
        var wBitmap = new Avalonia.Media.Imaging.Bitmap(skStream);
        context.DrawImage(wBitmap, rect);
    }

    /// <summary>
    /// Handle size changes.
    /// </summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _surfaceWidth = (float)e.NewSize.Width;
        _surfaceHeight = (float)e.NewSize.Height;
    }

    /// <summary>
    /// Handle pointer move for eye tracking.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        _cursorPosition = new Vector2((float)pos.X, (float)pos.Y);
        // Cursor tracking is handled by PinguAnimationSystem internally
    }

    /// <summary>
    /// Handle pointer press.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isPointerDown = true;
    }

    /// <summary>
    /// Handle pointer release.
    /// </summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPointerDown = false;
    }
}