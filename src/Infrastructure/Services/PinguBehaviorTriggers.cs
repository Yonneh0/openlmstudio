using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Pseudo-random behavior triggers for Pingu characters.
/// Implements weighted probability, duration ranges, and context awareness.
/// </summary>
public class PinguBehaviorTriggers
{
    private readonly ILogger<PinguBehaviorTriggers>? _logger;
    private readonly Random _random;
    private readonly PinguAnimationStateMachine _stateMachine;

    /// <summary>
    /// Behavior definition with weighted probability and duration ranges.
    /// </summary>
    public record struct BehaviorDefinition(
        PinguAnimationState State,
        float Probability,
        float MinDuration,
        float MaxDuration,
        Func<PinguAnimationState, bool>? ContextCheck);

    /// <summary>
    /// Active behaviors and their remaining duration.
    /// </summary>
    private readonly Dictionary<PinguAnimationState, float> _activeBehaviors;

    /// <summary>
    /// Default behavior definitions for a penguin.
    /// </summary>
    private static readonly BehaviorDefinition[] DefaultBehaviors = new[]
    {
        new BehaviorDefinition(PinguAnimationState.Twitch, 0.15f, 0.3f, 0.8f, null),
        new BehaviorDefinition(PinguAnimationState.HeadTurn, 0.1f, 0.5f, 1.5f, null),
        new BehaviorDefinition(PinguAnimationState.Scratch, 0.08f, 0.8f, 2.0f, (state) => !IsScratchingMidStride(state)),
        new BehaviorDefinition(PinguAnimationState.EarFlick, 0.05f, 0.2f, 0.5f, null),
        new BehaviorDefinition(PinguAnimationState.Blink, 0.3f, 0.1f, 0.3f, null),
        new BehaviorDefinition(PinguAnimationState.SittingDown, 0.02f, 3.0f, 10.0f, (state) => IsStableGround(state)),
        new BehaviorDefinition(PinguAnimationState.Breathing, 0.4f, 1.0f, 5.0f, null),
        new BehaviorDefinition(PinguAnimationState.Wave, 0.03f, 1.0f, 3.0f, null),
    };

    /// <summary>
    /// Last time each behavior was triggered.
    /// </summary>
    private readonly Dictionary<PinguAnimationState, double> _lastTriggerTime;

    /// <summary>
    /// Cooldown time between similar behaviors.
    /// </summary>
    private const double BehaviorCooldown = 2.0;

    public PinguBehaviorTriggers(
        PinguAnimationStateMachine stateMachine,
        ILogger<PinguBehaviorTriggers>? logger = null,
        Random? random = null)
    {
        _stateMachine = stateMachine;
        _logger = logger;
        _random = random ?? new Random();
        _activeBehaviors = new Dictionary<PinguAnimationState, float>();
        _lastTriggerTime = new Dictionary<PinguAnimationState, double>();
    }

    /// <summary>
    /// Update behavior triggers for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Update active behavior durations
        foreach (var (state, duration) in _activeBehaviors.ToList())
        {
            _activeBehaviors[state] -= deltaTime;
            if (_activeBehaviors[state] <= 0)
            {
                _activeBehaviors.Remove(state);
            }
        }

        // Check for new behavior triggers
        CheckBehaviors(deltaTime);
    }

    /// <summary>
    /// Check if a behavior can be triggered based on context and cooldown.
    /// </summary>
    public bool CanTriggerBehavior(PinguAnimationState behavior)
    {
        var definition = GetBehaviorDefinition(behavior);
        if (definition.ContextCheck == null) return true;

        return definition.ContextCheck(behavior);
    }

    /// <summary>
    /// Trigger a specific behavior immediately.
    /// </summary>
    public void TriggerBehavior(PinguAnimationState behavior)
    {
        var definition = GetBehaviorDefinition(behavior);
        var duration = (float)_random.NextDouble() * (definition.MaxDuration - definition.MinDuration) + definition.MinDuration;

        _activeBehaviors[behavior] = duration;
        _lastTriggerTime[behavior] = DateTime.UtcNow.TotalSeconds();

        _stateMachine.TriggerBehavior(behavior);
        _logger?.LogDebug("Triggered behavior: {Behavior} for {Duration:F1}s", behavior, duration);
    }

    /// <summary>
    /// Get the currently active behaviors.
    /// </summary>
    public IReadOnlyCollection<PinguAnimationState> ActiveBehaviors => _activeBehaviors.Keys;

    /// <summary>
    /// Get the remaining duration of a behavior.
    /// </summary>
    public float GetBehaviorDuration(PinguAnimationState behavior)
    {
        return _activeBehaviors.GetValueOrDefault(behavior, 0);
    }

    /// <summary>
    /// Reset all behaviors.
    /// </summary>
    public void Reset()
    {
        _activeBehaviors.Clear();
        _lastTriggerTime.Clear();
    }

    /// <summary>
    /// Check for new behavior triggers.
    /// </summary>
    private void CheckBehaviors(float deltaTime)
    {
        foreach (var definition in DefaultBehaviors)
        {
            if (_activeBehaviors.ContainsKey(definition.State)) continue;

            var lastTime = _lastTriggerTime.GetValueOrDefault(definition.State, 0);
            var timeSinceLastTrigger = DateTime.UtcNow.TotalSeconds() - lastTime;
            if (timeSinceLastTrigger < BehaviorCooldown) continue;

            if (!_stateMachine.IsInterruptible && definition.State != PinguAnimationState.Blink) continue;

            if (_random.NextDouble() < definition.Probability * deltaTime / 10)
            {
                if (definition.ContextCheck?.Invoke(definition.State) != false)
                {
                    TriggerBehavior(definition.State);
                }
            }
        }
    }

    /// <summary>
    /// Get the behavior definition for a state.
    /// </summary>
    private BehaviorDefinition GetBehaviorDefinition(PinguAnimationState state)
    {
        foreach (var d in DefaultBehaviors)
        {
            if (d.State == state) return d;
        }
        return new BehaviorDefinition(state, 0.1f, 0.5f, 2.0f, null);
    }

    /// <summary>
    /// Check if the penguin is stable enough to sit (not mid-stride).
    /// </summary>
    private static bool IsStableGround(PinguAnimationState currentState)
    {
        return currentState is PinguAnimationState.Idle or PinguAnimationState.Breathing or PinguAnimationState.Sit;
    }

    /// <summary>
    /// Check if the penguin is mid-stride (can't scratch).
    /// </summary>
    private static bool IsScratchingMidStride(PinguAnimationState currentState)
    {
        return currentState is PinguAnimationState.Walk or PinguAnimationState.Run;
    }
}

/// <summary>
/// Extension for getting total seconds from a DateTime.
/// </summary>
internal static class DateTimeExtensions
{
    public static double TotalSeconds(this DateTime dt) => (dt - DateTime.UnixEpoch).TotalSeconds;
}