using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;
using SkiaSharp;
using System.IO;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Event published by PinguStore for cross-component communication.
/// </summary>
public record PinguEvent(string Name);

/// <summary>
/// Static event bus for Pingu cross-component communication.
/// </summary>
public static class PinguEventBus
{
    public static event Action<PinguEvent>? OnEvent;

    public static void Publish(PinguEvent @event) => OnEvent?.Invoke(@event);
}

/// <summary>
/// Reactive state store for the Pingu System AI avatar.
/// </summary>
public class PinguStore : OpenLMStudio.Application.Interfaces.IPinguStore, IDisposable
{
    private readonly PinguState _state;
    private readonly ILogger<PinguStore>? _logger;
    private readonly Timer? _blinkTimer;
    private readonly object _stateLock = new();

    public PinguState State => _state;
    public event EventHandler<PinguStateChangedEventArgs>? OnStateChanged;

    /// <summary>
    /// Animation speed multiplier (1.0 = normal speed).
    /// </summary>
    public float AnimationSpeedMultiplier
    {
        get => _state.AnimationSpeedMultiplier;
        set => _state.AnimationSpeedMultiplier = value;
    }

    /// <summary>
    /// Behavior frequency multiplier (1.0 = normal frequency).
    /// </summary>
    public float BehaviorFrequencyMultiplier
    {
        get => _state.BehaviorFrequencyMultiplier;
        set => _state.BehaviorFrequencyMultiplier = value;
    }

    /// <summary>
    /// Pingu size multiplier (1.0 = normal size).
    /// </summary>
    public float PinguSizeMultiplier
    {
        get => _state.PinguSizeMultiplier;
        set => _state.PinguSizeMultiplier = value;
    }

    public PinguStore(ILogger<PinguStore>? logger = null)
    {
        _state = new PinguState();
        _logger = logger;
        _blinkTimer = new Timer(OnBlinkTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void NotifyChanged()
    {
        var args = new PinguStateChangedEventArgs(_state);
        OnStateChanged?.Invoke(this, args);
    }

    private void OnBlinkTimerTick(object? state)
    {
        _ = SetBlinkStateAsync(true);
        _blinkTimer?.Change(
            TimeSpan.FromSeconds(_state.BlinkIntervalMin +
                (DateTime.UtcNow.Millisecond / 1000.0) * (_state.BlinkIntervalMax - _state.BlinkIntervalMin)),
            Timeout.InfiniteTimeSpan);
    }

    public Task UpdateMoodAsync(PinguMood mood)
    {
        lock (_stateLock)
        {
            _state.Mood = mood;
            NotifyChanged();
        }
        return Task.CompletedTask;
    }

    public Task ToggleMenuAsync()
    {
        lock (_stateLock)
        {
            _state.IsMenuOpen = !_state.IsMenuOpen;
            if (!_state.IsMenuOpen)
                _state.ActivePanel = PinguPanelType.None;
            NotifyChanged();
        }
        var eventName = _state.IsMenuOpen ? "pingu-chat-open" : "pingu-chat-close";
        PinguEventBus.Publish(new PinguEvent(eventName));
        return Task.CompletedTask;
    }

    public Task SetActivePanelAsync(PinguPanelType panel)
    {
        lock (_stateLock)
        {
            _state.ActivePanel = panel;
            NotifyChanged();
        }
        return Task.CompletedTask;
    }

    public async Task SetAwakeAsync(bool awake)
    {
        lock (_stateLock)
        {
            _state.IsAwake = awake;
            NotifyChanged();
        }

        if (awake)
            _ = StartAwakeningSequenceAsync();
    }

    public async Task PinAndOpenChatAsync()
    {
        lock (_stateLock)
        {
            _state.IsVisible = true;
            _state.IsMenuOpen = true;
            _state.ActivePanel = PinguPanelType.About;
            NotifyChanged();
        }
        PinguEventBus.Publish(new PinguEvent("pingu-chat-open"));
        await Task.CompletedTask;
    }

    public async Task UnpinPinguAsync()
    {
        lock (_stateLock)
        {
            _state.IsVisible = false;
            _state.IsMenuOpen = false;
            _state.ActivePanel = PinguPanelType.None;
            NotifyChanged();
        }
        PinguEventBus.Publish(new PinguEvent("pingu-chat-close"));
        await Task.CompletedTask;
    }

    public Task SetLoadingProgressAsync(double progress)
    {
        lock (_stateLock)
        {
            _state.LoadProgress = progress;
            _state.IsLoadingModel = progress < 1.0;
            NotifyChanged();
        }
        return Task.CompletedTask;
    }

    public Task SetBlinkStateAsync(bool blinking)
    {
        lock (_stateLock)
        {
            _state.IsBlinking = blinking;
            NotifyChanged();
        }
        return Task.CompletedTask;
    }

    public async Task StartAwakeningSequenceAsync()
    {
        // Phase 1: Shake for 0.5s
        _state.AwakeningPhase = AwakeningPhase.Shake;
        NotifyChanged();
        await Task.Delay(500).ConfigureAwait(false);

        // Phase 2: Stretch for 2s
        _state.AwakeningPhase = AwakeningPhase.Stretch;
        NotifyChanged();
        await Task.Delay(2000).ConfigureAwait(false);

        // Phase 3: Glow for 2s
        _state.AwakeningPhase = AwakeningPhase.Glow;
        NotifyChanged();
        await Task.Delay(2000).ConfigureAwait(false);

        // Complete awakening
        _state.AwakeningPhase = AwakeningPhase.None;
        _state.IsAwake = true;
        NotifyChanged();

        _logger?.LogInformation("Pingu awakened successfully.");
        PinguEventBus.Publish(new PinguEvent("pingu-awakened"));
    }

    public Task StartBlinkTimerAsync()
    {
        double interval = _state.BlinkIntervalMin +
            (DateTime.UtcNow.Millisecond / 1000.0) * (_state.BlinkIntervalMax - _state.BlinkIntervalMin);
        _blinkTimer?.Change(TimeSpan.FromSeconds(interval), Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    public Task StartSpeakingAsync()
    {
        _state.Mood = PinguMood.Speaking;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public Task StartThinkingAsync()
    {
        _state.Mood = PinguMood.Thinking;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public async Task CompleteTaskAsync()
    {
        _state.Mood = PinguMood.Happy;
        NotifyChanged();
        await Task.Delay(2000).ConfigureAwait(false);
        _state.Mood = PinguMood.Idle;
        NotifyChanged();
    }

    public async Task HandleTaskErrorAsync()
    {
        _state.Mood = PinguMood.Error;
        NotifyChanged();
        await Task.Delay(5000).ConfigureAwait(false);
        _state.Mood = PinguMood.Idle;
        NotifyChanged();
    }

    public Task StartWorkingAsync()
    {
        _state.Mood = PinguMood.Working;
        _state.BobSpeed = 1.5;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public Task IdleAsync()
    {
        _state.Mood = PinguMood.Idle;
        _state.BobSpeed = 1.0;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _blinkTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a PinguRenderer for GPU rendering of the Pingu character.
    /// Returns a Func that renders the scene given a cursor position.
    /// </summary>
    public Func<System.Numerics.Vector2, SkiaSharp.SKBitmap, SkiaSharp.SKCanvas, Task> CreateRenderer()
    {
        // Generate all data using PinguMeshGenerator
        var generator = new PinguMeshGenerator();
        var characterData = generator.Generate();

        // Load bone hierarchy from JSON
        var loader = new PinguBoneLoader();
        var hierarchy = loader.LoadBoneHierarchy(
            System.Text.Json.JsonSerializer.Serialize(characterData.BoneHierarchy.Definitions,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));

        // Create animation system
        var animation = new PinguAnimationSystem(
            hierarchy,
            characterData.AnimationClips,
            characterData.PhysicsParams);

        // Create NPC manager
        var npcManager = new PinguNPCManager();

        // Load home scene
        var homeScene = loader.LoadHomeScene(
            System.Text.Json.JsonSerializer.Serialize(characterData.BoneHierarchy.Definitions,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false }));

        // Use texture atlas from character data
        var atlas = characterData.TextureAtlas ?? new byte[0];

        var renderer = new PinguRenderer(characterData.MeshData, hierarchy, animation, npcManager, homeScene, atlas);
        renderer.Initialize(400, 400);

        // Initialize animation state machine to Idle state
        animation.SetAnimationClip("Idle");

        return async (cursor, skBitmap, skCanvas) =>
        {
            // Update animation before rendering
            animation.Update(1f / 60f, cursor);
            renderer.Render(cursor, skBitmap, skCanvas);
            await Task.CompletedTask;
        };
    }
}
