using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Animation state machine for Pingu characters with seamless blending between states.
/// Supports concurrent animations without overlap conflicts.
/// </summary>
public class PinguAnimationStateMachine
{
    private readonly ILogger<PinguAnimationStateMachine>? _logger;
    private readonly Random _random;
    private readonly List<PinguAnimationClip> _clips;

    /// <summary>
    /// Current active animation state.
    /// </summary>
    public PinguAnimationState CurrentState { get; private set; }

    /// <summary>
    /// Blend factor between old and new clip (0.0 = old, 1.0 = new).
    /// </summary>
    public float BlendFactor { get; private set; }

    /// <summary>
    /// Duration of the blend transition.
    /// </summary>
    public float BlendDuration { get; set; } = 0.3f;

    /// <summary>
    /// Whether a blend is currently in progress.
    /// </summary>
    public bool IsBlending => BlendFactor > 0 && BlendFactor < 1;

    /// <summary>
    /// Current animation clip.
    /// </summary>
    public PinguAnimationClip? CurrentClip { get; private set; }

    /// <summary>
    /// Time since last state change.
    /// </summary>
    private float _stateTimer;

    /// <summary>
    /// Weighted behavior triggers for random behaviors.
    /// </summary>
    private readonly List<(PinguAnimationState State, float Weight)> _behaviorTriggers;

    /// <summary>
    /// Current behavior state (for concurrent behaviors).
    /// </summary>
    private PinguAnimationState _behaviorState;

    /// <summary>
    /// Priority levels for animations (higher = more important).
    /// </summary>
    private readonly Dictionary<PinguAnimationState, int> _animationPriorities;

    public PinguAnimationStateMachine(
        List<PinguAnimationClip> clips,
        ILogger<PinguAnimationStateMachine>? logger = null,
        Random? random = null)
    {
        _clips = clips;
        _logger = logger;
        _random = random ?? new Random();

        CurrentState = PinguAnimationState.Idle;
        _behaviorState = PinguAnimationState.Idle;
        _stateTimer = 0;
        BlendFactor = 1;

        _animationPriorities = new Dictionary<PinguAnimationState, int>
        {
            [PinguAnimationState.Idle] = 1,
            [PinguAnimationState.Breathing] = 2,
            [PinguAnimationState.Walk] = 3,
            [PinguAnimationState.Run] = 4,
            [PinguAnimationState.Sit] = 3,
            [PinguAnimationState.SittingDown] = 8,
            [PinguAnimationState.SittingUp] = 8,
            [PinguAnimationState.Wave] = 4,
            [PinguAnimationState.Scratch] = 5,
            [PinguAnimationState.Twitch] = 6,
            [PinguAnimationState.EarFlick] = 6,
            [PinguAnimationState.HeadTurn] = 5,
            [PinguAnimationState.Blink] = 7,
            [PinguAnimationState.SittingDown] = 8,
            [PinguAnimationState.SittingUp] = 8,
            [PinguAnimationState.Playing] = 3,
        };

        _behaviorTriggers = new List<(PinguAnimationState, float)>
        {
            (PinguAnimationState.Twitch, 0.15f),
            (PinguAnimationState.HeadTurn, 0.1f),
            (PinguAnimationState.Scratch, 0.08f),
            (PinguAnimationState.EarFlick, 0.05f),
            (PinguAnimationState.Blink, 0.3f),
            (PinguAnimationState.SittingDown, 0.02f),
            (PinguAnimationState.SittingUp, 0.02f),
        };
    }

    /// <summary>
    /// Transition to a new animation state.
    /// </summary>
    public void TransitionTo(PinguAnimationState newState)
    {
        if (newState == CurrentState) return;

        // Check priority
        var newPriority = _animationPriorities.GetValueOrDefault(newState, 1);
        var currentPriority = _animationPriorities.GetValueOrDefault(CurrentState, 1);

        if (newPriority < currentPriority && CurrentState != PinguAnimationState.Idle)
        {
            // Don't interrupt higher priority animations
            return;
        }

        // Find the clip for the new state
        var clip = _clips.FirstOrDefault(c => c.Name == newState.ToString());
        if (clip != null)
        {
            CurrentClip = clip;
            BlendFactor = 0;
            CurrentState = newState;
            _stateTimer = 0;
        }
    }

    /// <summary>
    /// Update the state machine for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        _stateTimer += deltaTime;

        // Update blend factor
        if (BlendFactor < 1)
        {
            BlendFactor = Math.Min(1, BlendFactor + deltaTime / BlendDuration);
        }

        // Check for behavior triggers
        CheckBehaviorTriggers(deltaTime);

        // Auto-transition from temporary states
        if (CurrentState is PinguAnimationState.Twitch or PinguAnimationState.EarFlick or PinguAnimationState.Blink)
        {
            if (_stateTimer > 0.5f)
            {
                TransitionTo(PinguAnimationState.Idle);
            }
        }
        else if (CurrentState is PinguAnimationState.SittingDown)
        {
            if (_stateTimer > 2.0f)
            {
                TransitionTo(PinguAnimationState.Sit);
            }
        }
        else if (CurrentState is PinguAnimationState.Sit)
        {
            if (_stateTimer > _random.Next(3000, 8000) / 1000f)
            {
                TransitionTo(PinguAnimationState.SittingUp);
            }
        }
    }

    /// <summary>
    /// Get the blended bone transformation for the current state.
    /// </summary>
    public (PinguAnimationClip? PrimaryClip, PinguAnimationClip? SecondaryClip, float BlendFactor) GetBlendedAnimation()
    {
        if (CurrentClip == null)
            return (null, null, 1);

        if (BlendFactor >= 1)
            return (CurrentClip, null, 1);

        return (CurrentClip, null, BlendFactor);
    }

    /// <summary>
    /// Trigger a specific behavior immediately.
    /// </summary>
    public void TriggerBehavior(PinguAnimationState behavior)
    {
        TransitionTo(behavior);
    }

    /// <summary>
    /// Check if the current state is interruptible.
    /// </summary>
    public bool IsInterruptible => _animationPriorities.GetValueOrDefault(CurrentState, 1) < 6;

    /// <summary>
    /// Reset the state machine to idle.
    /// </summary>
    public void Reset()
    {
        CurrentState = PinguAnimationState.Idle;
        _behaviorState = PinguAnimationState.Idle;
        BlendFactor = 1;
        _stateTimer = 0;
        CurrentClip = _clips.FirstOrDefault(c => c.Name == "Idle");
    }

    /// <summary>
    /// Check behavior triggers for random behaviors.
    /// </summary>
    private void CheckBehaviorTriggers(float deltaTime)
    {
        if (_random.NextDouble() > 0.01 * deltaTime) return;

        var cumulative = 0.0;
        foreach (var (state, weight) in _behaviorTriggers)
        {
            cumulative += weight;
            if (_random.NextDouble() < weight)
            {
                // Only trigger if not currently doing a conflicting behavior
                if (IsCompatible(CurrentState, state))
                {
                    TransitionTo(state);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Check if two animation states are compatible (can run concurrently).
    /// </summary>
    private bool IsCompatible(PinguAnimationState current, PinguAnimationState behavior)
    {
        // Can't scratch while mid-stride
        if (current is PinguAnimationState.Walk or PinguAnimationState.Run)
        {
            return behavior is PinguAnimationState.Twitch or PinguAnimationState.EarFlick or PinguAnimationState.Blink;
        }

        // Can't sit while already sitting
        if (current is PinguAnimationState.Sit)
        {
            return behavior is PinguAnimationState.Twitch or PinguAnimationState.EarFlick or PinguAnimationState.Blink or PinguAnimationState.SittingUp;
        }

        return true;
    }
}