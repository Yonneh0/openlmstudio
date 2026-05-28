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

    // Cached bone indices for performance
    private int _headBoneIndex = -1;
    private int _chestBoneIndex = -1;
    private int _leftEarBoneIndex = -1;
    private int _rightEarBoneIndex = -1;
    private bool _boneIndicesDirty = true;

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
        _boneIndicesDirty = true;

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

        // Invalidate cached bone indices when hierarchy changes
        if (_boneIndicesDirty)
        {
            _headBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "head");
            _chestBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "chest");
            _leftEarBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "leftEar");
            _rightEarBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "rightEar");
            _boneIndicesDirty = false;
        }

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

        // Apply IK (includes cursor tracking for eyes)
        ApplyInverseKinematics(deltaTime);

        // Apply body tilt toward cursor
        ApplyBodyTilt(deltaTime);

        // Apply random behaviors
        ApplyRandomBehaviors(deltaTime);

        // Apply breathing
        ApplyBreathing(deltaTime);

        // Recompute bone positions so GetBoneWorldPosition returns correct values
        RecomputeBonePositions();
    }

    /// <summary>
    /// Get the computed world position of a bone.
    /// </summary>
    public Vector2 GetBoneWorldPosition(int boneIndex)
    {
        // Return the cached position (populated by RecomputeBonePositions at end of Update)
        return new Vector2(_bonePositions[boneIndex * 3], _bonePositions[boneIndex * 3 + 1]);
    }

    /// <summary>
    /// Recompute bone positions from hierarchy + transforms.
    /// Walks up the parent chain accumulating offsets from each bone's local position.
    /// Roll/Pitch/Yaw are rotation angles and must NOT be applied as position offsets.
    /// </summary>
    private void RecomputeBonePositions()
    {
        var boneCount = _boneHierarchy.ResolvedBones.Count;
        for (var i = 0; i < boneCount; i++)
        {
            var bone = _boneHierarchy.ResolvedBones[i];
            var px = bone.X;
            var py = bone.Y;
            var pz = bone.Z;

            // Walk up the parent chain accumulating offsets, starting from the bone itself
            var current = bone;
            while (current.Parent != null)
            {
                current = current.Parent;
                px += current.X;
                py += current.Y;
                pz += current.Z;
            }

            _bonePositions[i * 3] = px;
            _bonePositions[i * 3 + 1] = py;
            _bonePositions[i * 3 + 2] = pz;
        }
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
                // Use SLERP for rotation (Roll/Pitch/Yaw) for smooth blending
                var roll = SlerpAngle(current.Roll, next.Roll, t);
                var pitch = SlerpAngle(current.Pitch, next.Pitch, t);
                var yaw = SlerpAngle(current.Yaw, next.Yaw, t);
                var scale = current.Scale + (next.Scale - current.Scale) * t;
                return new AnimationKeyframe
                {
                    Time = time,
                    Roll = roll,
                    Pitch = pitch,
                    Yaw = yaw,
                    Scale = scale,
                };
            }
        }

        return keyframes.LastOrDefault() ?? keyframes[0];
    }

    /// <summary>
    /// Spherical linear interpolation of an angle (in degrees), wrapping around at ±180°.
    /// </summary>
    private static float SlerpAngle(float from, float to, float t)
    {
        // Convert to radians
        var fromRad = from * Math.PI / 180f;
        var toRad = to * Math.PI / 180f;

        // Find the shortest rotation
        var diff = toRad - fromRad;
        while (diff > Math.PI) diff -= 2 * Math.PI;
        while (diff < -Math.PI) diff += 2 * Math.PI;

        // SLERP
        var result = fromRad + diff * t;

        // Convert back to degrees
        return (float)(result * 180f / Math.PI);
    }

    private void ApplyInverseKinematics(float deltaTime)
    {
        // Simple IK for head toward cursor - consolidates both IK and eye tracking into one method
        if (_cursorActive)
        {
            var headIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "head");
            if (headIndex >= 0)
            {
                var headPos = GetBoneWorldPosition(headIndex);
                var dx = _cursorPosition.X - headPos.X;
                var dy = _cursorPosition.Y - headPos.Y;
                var angle = (float)Math.Atan2(dy, dx);

                // IK stiffness for head rotation toward cursor
                _boneYaw[headIndex] += (angle - _boneYaw[headIndex]) * _physics.IkStiffness * deltaTime * 60f;
            }
        }
    }

    private void ApplyBodyTilt(float deltaTime)
    {
        // Use "chest" bone (not "torso") — the hierarchy defines it as "chest"
        var chestIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "chest");
        if (chestIndex >= 0 && _cursorActive)
        {
            var chestPos = GetBoneWorldPosition(chestIndex);
            var dx = _cursorPosition.X - chestPos.X;
            var targetTilt = (float)Math.Atan2(dx, 500f) * 0.3f;
            _boneRoll[chestIndex] += (targetTilt - _boneRoll[chestIndex]) * 0.05f * deltaTime * 60f;
        }
    }

    /// <summary>
    /// Apply random behavioral animations: twitches, head turns, scratches, ear flicks, blinks, and sitting.
    /// Probability thresholds and durations are documented as constants for maintainability.
    /// </summary>
    private void ApplyRandomBehaviors(float deltaTime)
    {
        // Cache bone indices to avoid repeated FindIndex calls
        var headIndex = _headBoneIndex;
        var chestIndex = _chestBoneIndex;
        var leftEarIndex = _leftEarBoneIndex;
        var rightEarIndex = _rightEarBoneIndex;
        var rightArmIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "rightArm");

        // Random twitch — ~0.1% chance per frame, lasts 0.3s
        if (_random.NextDouble() < 0.001f)
            TriggerTwitch();
        if (_currentTime - _lastTwitchTime < 0.3f)
            _boneRoll[headIndex >= 0 ? headIndex : 0] += (float)(_random.NextDouble() * 10 - 5);

        // Random head turn — ~0.2% chance per frame, lasts 1.0s
        if (_random.NextDouble() < 0.002f)
            TriggerHeadTurn();
        if (_currentTime - _lastHeadTurnTime < 1.0f && headIndex >= 0)
            _boneYaw[headIndex] += (float)(_random.NextDouble() * 20 - 10) * deltaTime;

        // Random scratch — ~0.05% chance per frame, lasts 2.0s
        if (_random.NextDouble() < 0.0005f)
            TriggerScratch();
        if (_currentTime - _lastScratchTime < 2.0f && rightArmIndex >= 0)
            _boneRoll[rightArmIndex] += (float)(_random.NextDouble() * 15 - 7) * deltaTime;

        // Random ear flick — ~0.3% chance per frame, lasts 0.5s
        if (_random.NextDouble() < 0.003f)
            TriggerEarFlick();
        if (_currentTime - _lastEarFlickTime < 0.5f)
        {
            if (leftEarIndex >= 0) _boneYaw[leftEarIndex] += (float)(_random.NextDouble() * 30 - 15) * deltaTime;
            if (rightEarIndex >= 0) _boneYaw[rightEarIndex] += (float)(_random.NextDouble() * 30 - 15) * deltaTime;
        }

        // Random blink — ~0.5% chance per frame, lasts 0.2s
        if (_random.NextDouble() < 0.005f)
            TriggerBlink();
        if (_currentTime - _lastBlinkTime < 0.2f && headIndex >= 0)
            _bonePitch[headIndex] += 20f * deltaTime;

        // Random sit down — ~0.01% chance per frame, sits for 5s
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