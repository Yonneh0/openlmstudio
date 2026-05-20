using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.Pingu;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Reactive state store for the Pingu System AI avatar.
/// </summary>
public class PinguStore : IPinguStore, IDisposable
{
    private readonly PinguState _state;
    private readonly ILogger<PinguStore>? _logger;
    private readonly Timer? _blinkTimer;
    private readonly object _stateLock = new();

    public PinguState State => _state;
    public event EventHandler<PinguStateChangedEventArgs>? OnStateChanged;

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
    }
}