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
    /// Creates a set of default animation clips (Idle, Walk, Sit, Wave, Twitch).
    /// </summary>
    public static List<PinguAnimationClip> CreateDefaultClips()
    {
        var defaultBoneNames = new[]
        {
            "root", "torso", "chest", "neck", "head",
            "leftArm", "leftForeArm", "leftHand",
            "rightArm", "rightForeArm", "rightHand",
            "leftUpLeg", "leftLeg", "leftFoot",
            "rightUpLeg", "rightLeg", "rightFoot",
            "leftEar", "rightEar",
        };
        var random = new Random(42);

        return new List<PinguAnimationClip>
        {
            new() { Name = "Idle", Duration = 2.0f, Looping = true, Tracks = CreateIdleTracks(defaultBoneNames, random) },
            new() { Name = "Walk", Duration = 0.8f, Looping = true, Tracks = CreateWalkTracks(defaultBoneNames, random) },
            new() { Name = "Sit", Duration = 1.5f, Looping = false, Tracks = CreateSitTracks(defaultBoneNames) },
            new() { Name = "Wave", Duration = 1.0f, Looping = true, Tracks = CreateWaveTracks(defaultBoneNames) },
            new() { Name = "Twitch", Duration = 0.3f, Looping = false, Tracks = CreateTwitchTracks(defaultBoneNames, random) },
        };
    }

    private static List<BoneAnimationTrack> CreateIdleTracks(string[] boneNames, Random random)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
        {
            tracks.Add(new BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(boneNames, bone),
                Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.5f, Roll = (float)(random.NextDouble() * 4 - 2), Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.5f, Roll = (float)(random.NextDouble() * 4 - 2), Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 2.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                },
            });
        }
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateWalkTracks(string[] boneNames, Random random)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
        {
            tracks.Add(new BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(boneNames, bone),
                Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.4f, Roll = (float)(random.NextDouble() * 10 - 5), Pitch = (float)(random.NextDouble() * 10 - 5), Yaw = 0, Scale = 1f },
                    new() { Time = 0.8f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                },
            });
        }
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateSitTracks(string[] boneNames)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
        {
            tracks.Add(new BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(boneNames, bone),
                Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.75f, Roll = 0, Pitch = 30, Yaw = 0, Scale = 1f },
                    new() { Time = 1.5f, Roll = 0, Pitch = 30, Yaw = 0, Scale = 1f },
                },
            });
        }
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateWaveTracks(string[] boneNames)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
        {
            var track = new BoneAnimationTrack { BoneIndex = Array.IndexOf(boneNames, bone) };
            if (bone == "rightArm" || bone == "rightForeArm")
            {
                track.Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.25f, Roll = -45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 0.5f, Roll = 45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 0.75f, Roll = -45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 45, Pitch = 10, Yaw = 0, Scale = 1f },
                };
            }
            else
            {
                track.Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                };
            }
            tracks.Add(track);
        }
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateTwitchTracks(string[] boneNames, Random random)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
        {
            tracks.Add(new BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(boneNames, bone),
                Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.15f, Roll = (float)(random.NextDouble() * 20 - 10), Pitch = (float)(random.NextDouble() * 10 - 5), Yaw = 0, Scale = 1f },
                    new() { Time = 0.3f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                },
            });
        }
        return tracks;
    }

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
