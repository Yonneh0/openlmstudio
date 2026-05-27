using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using PinguHomeSceneRenderer = OpenLMStudio.Infrastructure.Rendering.PinguHomeSceneRenderer;
using PinguRenderer = OpenLMStudio.Infrastructure.Rendering.PinguRenderer;
using PinguToolHolder = OpenLMStudio.Infrastructure.Services.PinguToolHolder;
using PinguAnimationSystem = OpenLMStudio.Infrastructure.Services.PinguAnimationSystem;
using PinguAnimationStateMachine = OpenLMStudio.Infrastructure.Services.PinguAnimationStateMachine;
using PinguBehaviorTriggers = OpenLMStudio.Infrastructure.Services.PinguBehaviorTriggers;
using PinguPhysicsSolver = OpenLMStudio.Infrastructure.Services.PinguPhysicsSolver;
using PinguInverseKinematics = OpenLMStudio.Infrastructure.Services.PinguInverseKinematics;
using PinguBoneHierarchy = OpenLMStudio.Domain.Models.PinguBoneHierarchy;
using PinguAnimationClip = OpenLMStudio.Domain.Models.PinguAnimationClip;
using PinguPhysicsParams = OpenLMStudio.Domain.Models.PinguPhysicsParams;
using PinguNPCManager = OpenLMStudio.Infrastructure.Services.PinguNPCManager;
using PinguBoneLoader = OpenLMStudio.Infrastructure.Services.PinguBoneLoader;
using PinguMeshGenerator = OpenLMStudio.Infrastructure.Services.PinguMeshGenerator;
using SKColors = SkiaSharp.SKColors;
using SKCanvas = SkiaSharp.SKCanvas;
using SKBitmap = SkiaSharp.SKBitmap;
using SKPaint = SkiaSharp.SKPaint;
using SKColorType = SkiaSharp.SKColorType;
using SKAlphaType = SkiaSharp.SKAlphaType;
using SKRect = SkiaSharp.SKRect;
using SKPaintStyle = SkiaSharp.SKPaintStyle;
using SKEncodedImageFormat = SkiaSharp.SKEncodedImageFormat;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pingu character view that renders the full character with animation, IK, and physics.
/// Uses SkiaSharp directly via Avalonia's DrawingContext with a continuous render loop.
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
    private readonly PinguRenderer _pinguRenderer;
    private readonly Random _random;

    private float _surfaceWidth;
    private float _surfaceHeight;
    private Vector2 _cursorPosition;
    private bool _isPointerDown;
    private bool _isInitialized;

    /// <summary>
    /// Cached bitmap for rendering to avoid allocation on every frame.
    /// </summary>
    private SKBitmap? _renderBitmap;

    /// <summary>
    /// DispatcherTimer for the continuous render loop.
    /// </summary>
    private DispatcherTimer? _renderTimer;

    /// <summary>
    /// Default constructor for XAML.
    /// Creates all required services and initializes the render loop.
    /// </summary>
    public PinguCharacterView()
    {
        _random = new Random();

        // Create the main renderer from generated character data
        var generator = new PinguMeshGenerator();
        var characterData = generator.Generate();
        var loader = new PinguBoneLoader();
        var hierarchy = loader.LoadBoneHierarchy(
            System.Text.Json.JsonSerializer.Serialize(characterData.BoneHierarchy.Definitions,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
        var animation = new PinguAnimationSystem(hierarchy, characterData.AnimationClips, characterData.PhysicsParams);
        var npcManager = new PinguNPCManager();
        var homeScene = loader.LoadHomeScene(
            System.Text.Json.JsonSerializer.Serialize(characterData.BoneHierarchy.Definitions,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));
        var atlas = characterData.TextureAtlas ?? Array.Empty<byte>();
        _pinguRenderer = new PinguRenderer(hierarchy, animation, npcManager, homeScene, atlas);
        _pinguRenderer.Initialize(400, 400);

        // Build IK inputs from resolved bones
        var boneCount = hierarchy.ResolvedBones.Count;
        var boneParents = hierarchy.ResolvedBones.Select(b => b.Parent?.Index ?? -1).ToArray();
        var bonePositions = hierarchy.ResolvedBones.Select(b => new Vector3(b.X, b.Y, b.Z)).ToArray();
        var boneRotations = hierarchy.ResolvedBones.Select(b => (float)(b.Roll * Math.PI / 180)).ToArray();
        _ik = new PinguInverseKinematics(boneCount, boneParents, bonePositions, boneRotations);

        _animationSystem = new PinguAnimationSystem(hierarchy, characterData.AnimationClips, characterData.PhysicsParams);
        _stateMachine = new PinguAnimationStateMachine(characterData.AnimationClips);
        _behaviorTriggers = new PinguBehaviorTriggers(_stateMachine);
        _physicsSolver = new PinguPhysicsSolver(boneCount, 0.01f, 0.9f);
        _toolHolder = new PinguToolHolder(hierarchy);
        _homeSceneRenderer = new PinguHomeSceneRenderer(homeScene);

        _isInitialized = true;

        // Wire up pointer handlers
        this.AddHandler(PointerPressedEvent, OnPointerPressed);
        this.AddHandler(PointerReleasedEvent, OnPointerReleased);
    }

    public PinguCharacterView(
        PinguAnimationSystem animationSystem,
        PinguAnimationStateMachine stateMachine,
        PinguBehaviorTriggers behaviorTriggers,
        PinguPhysicsSolver physicsSolver,
        PinguInverseKinematics ik,
        PinguToolHolder toolHolder,
        PinguHomeSceneRenderer homeSceneRenderer,
        PinguRenderer pinguRenderer,
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
        _pinguRenderer = pinguRenderer;
        _logger = logger;
        _random = random ?? new Random();
        _isInitialized = true;
    }

    /// <summary>
    /// Start the continuous render loop. Call from OnAttachedToVisualTree or after initialization.
    /// </summary>
    public void StartRenderLoop()
    {
        if (_renderTimer != null)
            return;

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16.67) // ~60fps
        };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    /// <summary>
    /// Stop the continuous render loop.
    /// </summary>
    public void StopRenderLoop()
    {
        _renderTimer?.Stop();
        _renderTimer = null;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        InvalidateVisual();
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

        // If pointer is held down, pause the animation
        if (_isPointerDown)
        {
            _stateMachine.Pause();
        }
    }

    /// <summary>
    /// Render the character to the canvas.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        if (_surfaceWidth <= 0 || _surfaceHeight <= 0) return;

        canvas.Clear(SKColors.Transparent);

        // Draw home scene
        _homeSceneRenderer.Render(canvas, _surfaceWidth, _surfaceHeight);

        // Draw penguin using the main renderer
        if (_pinguRenderer.RenderBitmap != null)
        {
            var bitmap = _pinguRenderer.RenderBitmap;
            var srcRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
            var dstRect = new SKRect(0, 0, _surfaceWidth, _surfaceHeight);
            canvas.DrawBitmap(bitmap, srcRect, dstRect);
        }

        // Draw tool attachments
        for (var i = 0; i < _toolHolder.Attachments.Count; i++)
        {
            var attachment = _toolHolder.Attachments[i];
            var toolPos = _toolHolder.GetToolPosition(i, GetBonePositions());
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
    /// Get bone positions from IK solver.
    /// </summary>
    private IReadOnlyList<Vector3> GetBonePositions()
    {
        var boneCount = _ik.BoneRotations.Length / 3;
        var positions = new List<Vector3>();
        for (var i = 0; i < boneCount; i++)
        {
            positions.Add(new Vector3(_ik.BonePositions[i * 3], _ik.BonePositions[i * 3 + 1], _ik.BonePositions[i * 3 + 2]));
        }
        return positions;
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

        // Reuse or create the bitmap if size changed
        if (_renderBitmap == null || _renderBitmap.Width != (int)_surfaceWidth || _renderBitmap.Height != (int)_surfaceHeight)
        {
            _renderBitmap?.Dispose();
            _renderBitmap = new SKBitmap((int)_surfaceWidth, (int)_surfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        }

        using var skCanvas = new SKCanvas(_renderBitmap);
        skCanvas.Clear(SKColors.Transparent);
        Render(skCanvas);

        var rect = new Avalonia.Rect(0, 0, _surfaceWidth, _surfaceHeight);
        using var skStream = new System.IO.MemoryStream();
        _renderBitmap!.Encode(skStream, SKEncodedImageFormat.Png, 90);
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

    /// <summary>
    /// Clean up resources.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopRenderLoop();
        _renderBitmap?.Dispose();
    }
}