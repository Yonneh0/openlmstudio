using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Reactive state store for the Pingu System AI avatar.
/// </summary>
public interface IPinguStore
{
    /// <summary>
    /// Current Pingu state.
    /// </summary>
    PinguState State { get; }

    /// <summary>
    /// Event fired when state changes.
    /// </summary>
    event EventHandler<PinguStateChangedEventArgs>? OnStateChanged;

    /// <summary>
    /// Updates Pingu's mood and notifies listeners.
    /// </summary>
    Task UpdateMoodAsync(PinguMood mood);

    /// <summary>
    /// Toggles Pingu's menu visibility.
    /// </summary>
    Task ToggleMenuAsync();

    /// <summary>
    /// Sets the active panel type when menu is open.
    /// </summary>
    Task SetActivePanelAsync(PinguPanelType panel);

    /// <summary>
    /// Sets Pingu's awake state and triggers awakening sequence if transitioning to true.
    /// </summary>
    Task SetAwakeAsync(bool awake);

    /// <summary>
    /// Sets loading progress during model load.
    /// </summary>
    Task SetLoadingProgressAsync(double progress);

    /// <summary>
    /// Triggers Pingu's blink state.
    /// </summary>
    Task SetBlinkStateAsync(bool blinking);

    /// <summary>
    /// Starts Pingu's awakening sequence (shake → stretch → glow).
    /// </summary>
    Task StartAwakeningSequenceAsync();

    /// <summary>
    /// Starts the random blink timer for idle Pingu.
    /// </summary>
    Task StartBlinkTimerAsync();

    /// <summary>
    /// Convenience: transitions Pingu to speaking state.
    /// </summary>
    Task StartSpeakingAsync();

    /// <summary>
    /// Convenience: transitions Pingu to thinking state.
    /// </summary>
    Task StartThinkingAsync();

    /// <summary>
    /// Convenience: transitions Pingu to happy state for 2 seconds.
    /// </summary>
    Task CompleteTaskAsync();

    /// <summary>
    /// Convenience: transitions Pingu to error state for 5 seconds.
    /// </summary>
    Task HandleTaskErrorAsync();

    /// <summary>
    /// Convenience: transitions Pingu to working state with 1.5x bob speed.
    /// </summary>
    Task StartWorkingAsync();

    /// <summary>
    /// Convenience: transitions Pingu to idle state.
    /// </summary>
    Task IdleAsync();

    /// <summary>
    /// Creates a PinguRenderer for GPU rendering of the Pingu character.
    /// Returns a function that renders the scene given a cursor position, bitmap, and canvas.
    /// </summary>
    Func<System.Numerics.Vector2, SkiaSharp.SKBitmap, SkiaSharp.SKCanvas, Task> CreateRenderer();
}
