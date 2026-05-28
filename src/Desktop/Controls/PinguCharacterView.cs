using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Rendering;
using OpenLMStudio.Infrastructure.Services;
using PinguHomeSceneRenderer = OpenLMStudio.Infrastructure.Rendering.PinguHomeSceneRenderer;
using SKBitmap = SkiaSharp.SKBitmap;
using SKCanvas = SkiaSharp.SKCanvas;
using SKColorType = SkiaSharp.SKColorType;
using SKAlphaType = SkiaSharp.SKAlphaType;
using SKPaint = SkiaSharp.SKPaint;
using SKColors = SkiaSharp.SKColors;
using SKRect = SkiaSharp.SKRect;
using SKEncodedImageFormat = SkiaSharp.SKEncodedImageFormat;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pingu character view that renders the full character with animation, IK, and physics.
/// Uses Avalonia's DrawingContext with a continuous render loop for zero-interop GPU rendering.
/// </summary>
public class PinguCharacterView : Control, IDisposable
{
    private readonly ILogger<PinguCharacterView>? _logger;
    private readonly Random _random;

    /// <summary>
    /// Cached JSON serialization options for compact (non-indented) serialization.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions _jsonCompactOptions = new()
    {
        WriteIndented = false
    };

    private float _surfaceWidth;
    private float _surfaceHeight;
    private Vector2 _cursorPosition;
    private bool _isPointerDown;
    private bool _isInitialized;
    private bool _isAttachedToVisualTree;

    /// <summary>
    /// Cached bitmap from the renderer for Avalonia rendering.
    /// </summary>
    private SKBitmap? _cachedBitmap;


    /// <summary>
    /// The main renderer for the Pingu character.
    /// </summary>
    private PinguRenderer? _pinguRenderer;

    /// <summary>
    /// Home scene renderer for the penguin's home area.
    /// </summary>
    private PinguHomeSceneRenderer? _homeSceneRenderer;

    /// <summary>
    /// Animation state machine for the character.
    /// </summary>
    private PinguAnimationStateMachine? _stateMachine;

    /// <summary>
    /// Behavior triggers for pseudo-random lifelike movements.
    /// </summary>
    private PinguBehaviorTriggers? _behaviorTriggers;

    /// <summary>
    /// Tool holder for managing tool attachments.
    /// </summary>
    private PinguToolHolder? _toolHolder;

    /// <summary>
    /// DispatcherTimer for the continuous render loop.
    /// </summary>
    private DispatcherTimer? _renderTimer;

    /// <summary>
    /// Flag to track disposal state.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Animation speed multiplier (1.0 = normal speed).
    /// </summary>
    private float _animationSpeedMultiplier = 1.0f;

    /// <summary>
    /// Default constructor for XAML.
    /// Creates all required services and initializes the render loop.
    /// </summary>
    public PinguCharacterView()
    {
        _random = new Random();
        InitFromGeneratedData();
        _isInitialized = true;
        _isAttachedToVisualTree = false;
    }

    /// <summary>
    /// Parameterized constructor for DI.
    /// </summary>
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
        _pinguRenderer = pinguRenderer;
        _homeSceneRenderer = homeSceneRenderer;
        _stateMachine = stateMachine;
        _behaviorTriggers = behaviorTriggers;
        _toolHolder = toolHolder;
        _logger = logger;
        _random = random ?? new Random();
        _isInitialized = true;
        _isAttachedToVisualTree = false;
    }

    /// <summary>
    /// Initialize all services from generated character data (used by default constructor).
    /// </summary>
    private void InitFromGeneratedData()
    {
        try
        {
            var generator = new PinguMeshGenerator();
            var characterData = generator.Generate();
            var loader = new PinguBoneLoader();
            var hierarchy = loader.LoadBoneHierarchy(
                System.Text.Json.JsonSerializer.Serialize(characterData.BoneHierarchy.Definitions,
                    _jsonCompactOptions));

            // Create the main renderer from generated character data
            var animation = new PinguAnimationSystem(hierarchy, characterData.AnimationClips, characterData.PhysicsParams);
            var npcManager = new PinguNPCManager();
            var homeScene = loader.LoadHomeScene(
                System.Text.Json.JsonSerializer.Serialize(characterData.BoneHierarchy.Definitions,
                    _jsonCompactOptions));
            var atlas = characterData.TextureAtlas ?? Array.Empty<byte>();
            _pinguRenderer = new PinguRenderer(characterData.MeshData, hierarchy, animation, npcManager, homeScene, atlas);
            _pinguRenderer.Initialize(400, 400);

            // Create remaining services (reusing hierarchy)
            _stateMachine = new PinguAnimationStateMachine(characterData.AnimationClips);
            _behaviorTriggers = new PinguBehaviorTriggers(_stateMachine);
            _toolHolder = new PinguToolHolder(hierarchy);
            _homeSceneRenderer = new PinguHomeSceneRenderer(homeScene);

            // Initialize animation state machine to Idle state
            _stateMachine?.TransitionTo(PinguAnimationState.Idle);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize PinguCharacterView from generated data");
        }
    }

    /// <summary>
    /// Start the continuous render loop. Call from OnAttachedToVisualTree.
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
        // Update animation state before rendering
        Update(1f / 60f);
        InvalidateVisual();
    }

    /// <summary>
    /// Update the character for one frame. Call from render loop before Render().
    /// </summary>
    public void Update(float deltaTime)
    {
        var adjustedDeltaTime = deltaTime * _animationSpeedMultiplier;
        _stateMachine?.Update(adjustedDeltaTime);
        _behaviorTriggers?.Update(adjustedDeltaTime);
        _toolHolder?.Update(adjustedDeltaTime);

        // If pointer is held down, pause the animation
        if (_isPointerDown)
        {
            _stateMachine?.Pause();
        }
        else
        {
            // Resume if previously paused
            if (_stateMachine?.IsPaused == true)
            {
                _stateMachine?.Resume();
            }
        }
    }

    /// <summary>
    /// Override Render to draw with SkiaSharp on the DrawingContext.
    /// Uses PinguRenderer.Render() for all rendering (home scene, penguin, tools).
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (_disposed || !_isInitialized || !_isAttachedToVisualTree)
            return;

        if (_surfaceWidth <= 0 || _surfaceHeight <= 0)
            return;

        // Use the renderer's bitmap and canvas
        var bitmap = _pinguRenderer?.RenderBitmap;
        var canvas = _pinguRenderer?.Canvas;
        if (bitmap == null || canvas == null)
            return;

        // Render the scene (this also updates the animation internally)
        _pinguRenderer?.Render(_cursorPosition, bitmap, canvas);

        // Dispose the previous cached bitmap before creating a new one
        var oldBitmap = _cachedBitmap;
        _cachedBitmap = new SKBitmap((int)_surfaceWidth, (int)_surfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        // Copy the renderer's bitmap to our cached bitmap
        using var dstCanvas = new SKCanvas(_cachedBitmap);
        dstCanvas.DrawBitmap(bitmap, 0, 0);

        // Copy the cached bitmap to the Avalonia DrawingContext
        using var skStream = new System.IO.MemoryStream();
        _cachedBitmap.Encode(skStream, SKEncodedImageFormat.Png, 90);
        skStream.Position = 0;
        using var wBitmap = new Avalonia.Media.Imaging.Bitmap(skStream);
        var rect = new Avalonia.Rect(0, 0, _surfaceWidth, _surfaceHeight);
        context.DrawImage(wBitmap, rect);

        // Dispose old bitmap after rendering completes (to avoid race conditions)
        oldBitmap?.Dispose();
    }

    /// <summary>
    /// Handle size changes.
    /// </summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _surfaceWidth = (float)e.NewSize.Width;
        _surfaceHeight = (float)e.NewSize.Height;

        // Resize the renderer if it's initialized
        if (_pinguRenderer != null && _isInitialized)
        {
            _pinguRenderer.Resize((int)_surfaceWidth, (int)_surfaceHeight);
        }
    }

    /// <summary>
    /// Handle pointer move for eye tracking.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        _cursorPosition = new Vector2((float)pos.X, (float)pos.Y);
    }

    /// <summary>
    /// Handle pointer press.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isPointerDown = true;
    }

    /// <summary>
    /// Handle pointer release.
    /// </summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPointerDown = false;
    }

    /// <summary>
    /// Called when this control is attached to the visual tree.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        StartRenderLoop();
    }

    /// <summary>
    /// Called when this control is detached from the visual tree.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttachedToVisualTree = false;
        StopRenderLoop();
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
    }

    /// <summary>
    /// Set the animation speed multiplier.
    /// </summary>
    public void SetAnimationSpeed(float speed)
    {
        _animationSpeedMultiplier = Math.Max(0.1f, Math.Min(3.0f, speed));
    }

    /// <summary>
    /// Get the current animation speed multiplier.
    /// </summary>
    public float GetAnimationSpeed() => _animationSpeedMultiplier;

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopRenderLoop();
        _pinguRenderer?.Dispose();
        _cachedBitmap?.Dispose();
        _cachedBitmap = null;
    }
}