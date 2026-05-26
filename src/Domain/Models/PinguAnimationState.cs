namespace OpenLMStudio.Domain.Models;

/// <summary>
/// The current animation state of a Pingu character.
/// </summary>
public enum PinguAnimationState
{
    Idle,
    Walk,
    Run,
    Sit,
    Wave,
    Scratch,
    Twitch,
    EarFlick,
    HeadTurn,
    Blink,
    SittingDown,
    SittingUp,
    Playing,
    Breathing,
}

/// <summary>
/// Configuration for a specific animation state.
/// </summary>
public class PinguAnimationStateConfig
{
    public PinguAnimationState State { get; set; }

    /// <summary>
    /// The animation clip name for this state.
    /// </summary>
    public string? AnimationClipName { get; set; }

    /// <summary>
    /// How long this state typically lasts (in seconds).
    /// </summary>
    public float DurationMin { get; set; } = 1.0f;
    public float DurationMax { get; set; } = 3.0f;

    /// <summary>
    /// Blend speed when transitioning to/from this state.
    /// </summary>
    public float BlendSpeed { get; set; } = 0.15f;

    /// <summary>
    /// Whether this state can be interrupted by higher-priority states.
    /// </summary>
    public bool Interruptible { get; set; } = true;

    /// <summary>
    /// Priority of this state (higher = more important, less likely to be interrupted).
    /// </summary>
    public int Priority { get; set; } = 0;
}