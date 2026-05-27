namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A single keyframe for a bone in an animation clip.
/// </summary>
public class AnimationKeyframe
{
    /// <summary>
    /// The time position of this keyframe (in seconds).
    /// </summary>
    public float Time { get; set; }

    /// <summary>
    /// Bone position (relative to parent).
    /// </summary>
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    /// <summary>
    /// Bone rotation in Euler angles (degrees).
    /// </summary>
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }

    /// <summary>
    /// Bone scale.
    /// </summary>
    public float Scale { get; set; } = 1.0f;
}

/// <summary>
/// Animation keyframes for a single bone.
/// </summary>
public class BoneAnimationTrack
{
    /// <summary>
    /// The bone index this track belongs to.
    /// </summary>
    public int BoneIndex { get; set; }

    /// <summary>
    /// The keyframes for this bone.
    /// </summary>
    public List<AnimationKeyframe> Keyframes { get; set; } = new();
}

/// <summary>
/// A complete animation clip for a character.
/// </summary>
public class PinguAnimationClip
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total duration of the animation clip (in seconds).
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// Whether the animation loops.
    /// </summary>
    public bool Looping { get; set; } = true;

    /// <summary>
    /// Tracks for each bone in the animation.
    /// </summary>
    public List<BoneAnimationTrack> Tracks { get; set; } = new();

    /// <summary>
    /// Get the total number of keyframes across all tracks.
    /// </summary>
    public int TotalKeyframes => Tracks.Sum(t => t.Keyframes.Count);

    /// <summary>
    /// Get the maximum number of keyframes in any single track.
    /// </summary>
    public int MaxTrackKeyframes => Tracks.Count > 0 ? Tracks.Max(t => t.Keyframes.Count) : 0;

    /// <summary>
    /// Get the number of unique time steps in this animation (max keyframes across all tracks).
    /// <remarks>This is the actual number of animation ticks — use this instead of TotalKeyframes for timing calculations.</remarks>
    /// </summary>
    public int KeyframeCount => MaxTrackKeyframes;

    /// <summary>
    /// Sort all tracks' keyframes by time in ascending order.
    /// </summary>
    public void SortKeyframes()
    {
        foreach (var track in Tracks)
            track.Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
}
