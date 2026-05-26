using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Real-time animation and physics solver for Pingu characters.
/// Supports IK, bone transforms, animation blending, and physics.
/// </summary>
public class PinguAnimationSystem
{
    private readonly PinguBoneHierarchy _boneHierarchy;
    private readonly List<PinguAnimationClip> _animationClips;
    private readonly PinguPhysicsParams _physics;
    private readonly ILogger<PinguAnimationSystem>? _logger;
    private readonly Random _random;
    private float _currentTime;
    private Vector2 _cursorPosition;
    private bool _cursorActive;

    // Current bone transforms (world space)
    private readonly float[] _bonePositions;
    private readonly float[] _boneRoll;
    private readonly float[] _bonePitch;
    private readonly float[] _boneYaw;
    private readonly float[] _boneScale;

    // IK targets for limbs
    private readonly Vector2[] _ikTargets;
    private readonly bool[] _ikActive;

    // Animation state
    private string _currentClipName = "Idle";
#pragma warning disable CS0414 // Field is assigned but never read (used for future animation blending)
    private float _animationBlend;
#pragma warning restore CS0414
    private float _lastTwitchTime;
    private float _lastHeadTurnTime;
    private float _lastScratchTime;
    private float _lastEarFlickTime;
    private float _lastBlinkTime;
    private float _lastSitDownTime;
    private bool _isSitting;

    // Eye tracking
    private readonly float[] _eyeTargetAngles;
    private readonly float[] _eyeCurrentAngles;

    public PinguAnimationSystem(
        PinguBoneHierarchy boneHierarchy,
        List<PinguAnimationClip> animationClips,
        PinguPhysicsParams physics,
        ILogger<PinguAnimationSystem>? logger = null,
        Random? random = null)
    {
        _boneHierarchy = boneHierarchy;
        _animationClips = animationClips;
        _physics = physics;
        _logger = logger;
        _random = random ?? new Random();
        _boneHierarchy.Resolve();

        var boneCount = boneHierarchy.ResolvedBones.Count;
        _bonePositions = new float[boneCount * 3];
        _boneRoll = new float[boneCount];
        _bonePitch = new float[boneCount];
        _boneYaw = new float[boneCount];
        _boneScale = new float[boneCount];
        _ikTargets = new Vector2[boneCount];
        _ikActive = new bool[boneCount];
        _eyeTargetAngles = new float[boneCount];
        _eyeCurrentAngles = new float[boneCount];
    }

    /// <summary>
    /// Update the animation state for one frame.
    /// </summary>
    public void Update(float deltaTime, Vector2? cursorPosition = null)
    {
        _currentTime += deltaTime;

        if (cursorPosition.HasValue)
        {
            _cursorPosition = cursorPosition.Value;
            _cursorActive = true;
        }

        var currentClip = GetCurrentClip();
        var tracks = currentClip?.Tracks ?? new List<BoneAnimationTrack>();

        // Update bone transforms from animation
        for (var i = 0; i < _boneHierarchy.ResolvedBones.Count; i++)
        {
            var track = tracks.FirstOrDefault(t => t.BoneIndex == i);
            if (track != null)
            {
                var keyframes = track.Keyframes.OrderBy(k => k.Time).ToList();
                var time = _currentTime % currentClip!.Duration;
                var keyframe = InterpolateKeyframes(keyframes, time);

                _boneRoll[i] = keyframe.Roll;
                _bonePitch[i] = keyframe.Pitch;
                _boneYaw[i] = keyframe.Yaw;
                _boneScale[i] = keyframe.Scale;
            }
        }

        // Apply IK
        ApplyInverseKinematics(deltaTime);

        // Apply cursor tracking (eyes follow cursor)
        ApplyEyeTracking(deltaTime);

        // Apply body tilt toward cursor
        ApplyBodyTilt(deltaTime);

        // Apply random behaviors
        ApplyRandomBehaviors(deltaTime);

        // Apply breathing
        ApplyBreathing(deltaTime);
    }

    /// <summary>
    /// Get the computed world position of a bone.
    /// </summary>
    public Vector2 GetBoneWorldPosition(int boneIndex)
    {
        var bone = _boneHierarchy.ResolvedBones[boneIndex];
        var parent = bone.Parent;
        var px = parent != null ? _bonePositions[parent.Index * 3] : 0;
        var py = parent != null ? _bonePositions[parent.Index * 3 + 1] : 0;
        return new Vector2(px + _bonePositions[boneIndex * 3], py + _bonePositions[boneIndex * 3 + 1]);
    }

    /// <summary>
    /// Set IK target for a bone.
    /// </summary>
    public void SetIKTarget(int boneIndex, Vector2 target)
    {
        _ikTargets[boneIndex] = target;
        _ikActive[boneIndex] = true;
    }

    /// <summary>
    /// Clear IK target for a bone.
    /// </summary>
    public void ClearIKTarget(int boneIndex)
    {
        _ikActive[boneIndex] = false;
    }

    /// <summary>
    /// Trigger a random twitch.
    /// </summary>
    public void TriggerTwitch()
    {
        _lastTwitchTime = _currentTime;
    }

    /// <summary>
    /// Trigger a head turn.
    /// </summary>
    public void TriggerHeadTurn()
    {
        _lastHeadTurnTime = _currentTime;
    }

    /// <summary>
    /// Trigger a scratch.
    /// </summary>
    public void TriggerScratch()
    {
        _lastScratchTime = _currentTime;
    }

    /// <summary>
    /// Trigger an ear flick.
    /// </summary>
    public void TriggerEarFlick()
    {
        _lastEarFlickTime = _currentTime;
    }

    /// <summary>
    /// Trigger a blink.
    /// </summary>
    public void TriggerBlink()
    {
        _lastBlinkTime = _currentTime;
    }

    /// <summary>
    /// Trigger sitting down.
    /// </summary>
    public void TriggerSitDown()
    {
        _isSitting = true;
        _lastSitDownTime = _currentTime;
    }

    /// <summary>
    /// Trigger sitting up.
    /// </summary>
    public void TriggerSitUp()
    {
        _isSitting = false;
    }

    /// <summary>
    /// Set the current animation clip.
    /// </summary>
    public void SetAnimationClip(string clipName)
    {
        _currentClipName = clipName;
        _animationBlend = 0f;
    }

    /// <summary>
    /// Get the current animation clip.
    /// </summary>
    private PinguAnimationClip? GetCurrentClip()
    {
        return _animationClips.FirstOrDefault(c => c.Name == _currentClipName) ?? _animationClips.FirstOrDefault();
    }

    private AnimationKeyframe InterpolateKeyframes(List<AnimationKeyframe> keyframes, float time)
    {
        if (keyframes.Count == 0)
            return new();

        if (keyframes.Count == 1)
            return keyframes[0];

        for (var i = 0; i < keyframes.Count - 1; i++)
        {
            var current = keyframes[i];
            var next = keyframes[i + 1];
            if (time >= current.Time && time <= next.Time)
            {
                var t = (time - current.Time) / (next.Time - current.Time);
                return new AnimationKeyframe
                {
                    Time = time,
                    Roll = current.Roll + (next.Roll - current.Roll) * t,
                    Pitch = current.Pitch + (next.Pitch - current.Pitch) * t,
                    Yaw = current.Yaw + (next.Yaw - current.Yaw) * t,
                    Scale = current.Scale + (next.Scale - current.Scale) * t,
                };
            }
        }

        return keyframes.LastOrDefault() ?? keyframes[0];
    }

    private void ApplyInverseKinematics(float deltaTime)
    {
        // Simple IK for head toward cursor
        if (_cursorActive)
        {
            var headIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "head");
            if (headIndex >= 0)
            {
                var headPos = GetBoneWorldPosition(headIndex);
                var dx = _cursorPosition.X - headPos.X;
                var dy = _cursorPosition.Y - headPos.Y;
                var angle = (float)Math.Atan2(dy, dx);

                _boneYaw[headIndex] += (angle - _boneYaw[headIndex]) * _physics.IkStiffness * deltaTime * 60f;
            }
        }
    }

    private void ApplyEyeTracking(float deltaTime)
    {
        var leftEarIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "leftEar");
        var rightEarIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "rightEar");

        if (_cursorActive)
        {
            var headPos = GetBoneWorldPosition(Math.Max(leftEarIndex, rightEarIndex));
            var dx = _cursorPosition.X - headPos.X;
            var dy = _cursorPosition.Y - headPos.Y;
            var angle = (float)Math.Atan2(dy, dx);

            if (leftEarIndex >= 0)
                _boneYaw[leftEarIndex] += (angle - _boneYaw[leftEarIndex]) * 0.1f * deltaTime * 60f;
            if (rightEarIndex >= 0)
                _boneYaw[rightEarIndex] += (angle - _boneYaw[rightEarIndex]) * 0.1f * deltaTime * 60f;
        }
    }

    private void ApplyBodyTilt(float deltaTime)
    {
        var spineIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "spine");
        if (spineIndex >= 0 && _cursorActive)
        {
            var spinePos = GetBoneWorldPosition(spineIndex);
            var dx = _cursorPosition.X - spinePos.X;
            var targetTilt = (float)Math.Atan2(dx, 500f) * 0.3f;
            _boneRoll[spineIndex] += (targetTilt - _boneRoll[spineIndex]) * 0.05f * deltaTime * 60f;
        }
    }

    private void ApplyRandomBehaviors(float deltaTime)
    {
        // Random twitch
        if (_random.NextDouble() < 0.001f)
            TriggerTwitch();
        if (_currentTime - _lastTwitchTime < 0.3f)
            _boneRoll[0] += (float)(_random.NextDouble() * 10 - 5);

        // Random head turn
        if (_random.NextDouble() < 0.002f)
            TriggerHeadTurn();
        if (_currentTime - _lastHeadTurnTime < 1.0f)
            _boneYaw[4] += (float)(_random.NextDouble() * 20 - 10) * deltaTime;

        // Random scratch
        if (_random.NextDouble() < 0.0005f)
            TriggerScratch();
        if (_currentTime - _lastScratchTime < 2.0f)
            _boneRoll[8] += (float)(_random.NextDouble() * 15 - 7) * deltaTime;

        // Random ear flick
        if (_random.NextDouble() < 0.003f)
            TriggerEarFlick();
        if (_currentTime - _lastEarFlickTime < 0.5f)
        {
            var leftEar = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "leftEar");
            var rightEar = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "rightEar");
            if (leftEar >= 0) _boneYaw[leftEar] += (float)(_random.NextDouble() * 30 - 15) * deltaTime;
            if (rightEar >= 0) _boneYaw[rightEar] += (float)(_random.NextDouble() * 30 - 15) * deltaTime;
        }

        // Random blink
        if (_random.NextDouble() < 0.005f)
            TriggerBlink();
        if (_currentTime - _lastBlinkTime < 0.2f)
        {
            var headIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "head");
            if (headIndex >= 0) _bonePitch[headIndex] += 20f * deltaTime;
        }

        // Random sit down
        if (_random.NextDouble() < 0.0001f && !_isSitting)
            TriggerSitDown();
        if (_isSitting && _currentTime - _lastSitDownTime > 5f)
            TriggerSitUp();
    }

    private void ApplyBreathing(float deltaTime)
    {
        var chestIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "chest");
        if (chestIndex >= 0)
        {
            var breath = (float)(Math.Sin(_currentTime * 2f) * 0.5f + 0.5f);
            _boneScale[chestIndex] = 1.0f + breath * 0.05f;
        }
    }
}