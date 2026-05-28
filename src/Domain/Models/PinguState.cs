namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Represents the current mood of the Pingu avatar.
/// </summary>
public enum PinguMood
{
    Idle,
    Thinking,
    Speaking,
    Happy,
    Error,
    Working
}

/// <summary>
/// The panel currently displayed when Pingu's menu is open.
/// </summary>
public enum PinguPanelType
{
    Skills,
    Settings,
    Models,
    Compile,
    Logs,
    About,
    None
}

/// <summary>
/// The phase of Pingu's awakening animation sequence.
/// </summary>
public enum AwakeningPhase
{
    None,
    Shake,
    Stretch,
    Glow
}

/// <summary>
/// Configuration for the Pingu avatar's visual appearance and animation behavior.
/// </summary>
public class PinguAvatarConfig
{
    public double BobSpeed { get; set; } = 1.0;
    public double BlinkIntervalMin { get; set; } = 1.5;
    public double BlinkIntervalMax { get; set; } = 3.0;
    public double BlinkDuration { get; set; } = 0.2;
    public double SpeakingFrameRate { get; set; } = 10.0;
}

/// <summary>
/// Reactive state machine for the Pingu System AI avatar.
/// </summary>
public class PinguState
{
    public PinguMood Mood { get; set; } = PinguMood.Idle;
    public bool IsVisible { get; set; } = true;
    public bool IsMenuOpen { get; set; }
    public PinguPanelType ActivePanel { get; set; } = PinguPanelType.None;
    public bool IsBlinking { get; set; }
    public int MouthFrame { get; set; }
    public double BobSpeed { get; set; } = 1.0;
    public double BlinkIntervalMin { get; set; } = 1.5;
    public double BlinkIntervalMax { get; set; } = 3.0;

    // Animation and appearance multipliers (exposed via PinguStore)
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;
    public float BehaviorFrequencyMultiplier { get; set; } = 1.0f;
    public float PinguSizeMultiplier { get; set; } = 1.0f;

    // Awakening state
    public bool IsAwake { get; set; }
    public bool HasGguf { get; set; }
    public bool HasLlamaCpp { get; set; }
    public AwakeningPhase AwakeningPhase { get; set; } = AwakeningPhase.None;
    public bool IsLoadingModel { get; set; }
    public double LoadProgress { get; set; }

    public void Reset()
    {
        Mood = PinguMood.Idle;
        IsVisible = true;
        IsMenuOpen = false;
        ActivePanel = PinguPanelType.None;
        IsBlinking = false;
        MouthFrame = 0;
        BobSpeed = 1.0;
        BlinkIntervalMin = 1.5;
        BlinkIntervalMax = 3.0;
        AwakeningPhase = AwakeningPhase.None;
        IsLoadingModel = false;
        LoadProgress = 0;
    }
}

/// <summary>
/// Event args for Pingu state changes.
/// </summary>
public class PinguStateChangedEventArgs : EventArgs
{
    public PinguState State { get; }
    public PinguStateChangedEventArgs(PinguState state) => State = state;
}
