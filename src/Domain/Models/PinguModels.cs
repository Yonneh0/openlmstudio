namespace OpenLMStudio.Domain.Models;

// ============================================================================
// Enums
// ============================================================================

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
/// The role of a Pingu character in the NPC system.
/// </summary>
public enum PinguRole
{
    Idle,
    Worker,
    Explorer,
    Assistant,
    Guardian,
    Artist,
    Chef,
    Scientist,
    Wandering,
}

/// <summary>
/// The type of tool a Pingu is holding.
/// </summary>
public enum PinguToolType
{
    None,
    Pickaxe,
    Sledgehammer,
    PokeStick,
    Paintbrush,
    ChefHat,
    LabCoat,
    Crown,
    Hat,
}

/// <summary>
/// Types of tasks Pingu can be assigned to orchestrate.
/// </summary>
public enum PinguTaskType
{
    TaskOrchestration,
    UIControl,
    ModelManagement,
    GamePlay,
    Wandering,
    UserAssistant,
    ModelRun,
    Workflow,
}

/// <summary>
/// The current status of a Pingu task.
/// </summary>
public enum PinguTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// The current operational state of a Pingu NPC.
/// </summary>
public enum PinguNPCState
{
    Idle,
    Moving,
    Working,
    Playing,
    Eating,
    Sleeping,
    Wandering,
}

/// <summary>
/// Type of hat worn by a Pingu character.
/// </summary>
public enum PinguHatType
{
    None,
    Crown,
    Cap,
    Beanie,
    ChefHat,
    LabCoat,
    Hat,
}

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
    Working,
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
    None,
}

/// <summary>
/// The phase of Pingu's awakening animation sequence.
/// </summary>
public enum AwakeningPhase
{
    None,
    Shake,
    Stretch,
    Glow,
}

// ============================================================================
// Accessories
// ============================================================================

/// <summary>
/// Shared base class for items that attach to a Pingu's bone hierarchy (hats, tools, etc.).
/// </summary>
public abstract class PinguAccessory
{
    public string Name { get; set; } = string.Empty;
    public int AttachedBoneIndex { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Scale { get; set; } = 1.0f;
    public string Color { get; set; } = "#FFFFFF";

    public PinguAccessory Clone() => (PinguAccessory)MemberwiseClone();
}

/// <summary>
/// A hat that can be worn by a Pingu character.
/// <remarks>Extends PinguAccessory for bone attachment and transform offsets.</remarks>
/// </summary>
public class PinguHat : PinguAccessory
{
    public PinguHatType HatType { get; set; }
    public string PutOnAnimation { get; set; } = "hatPutOn";
    public string TakeOffAnimation { get; set; } = "hatTakeOff";
    public bool IsEquipped { get; set; }

    public new PinguHat Clone() => new()
    {
        Name = Name,
        HatType = HatType,
        AttachedBoneIndex = AttachedBoneIndex,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        OffsetZ = OffsetZ,
        Roll = Roll,
        Pitch = Pitch,
        Yaw = Yaw,
        Scale = Scale,
        Color = Color,
        PutOnAnimation = PutOnAnimation,
        TakeOffAnimation = TakeOffAnimation,
        IsEquipped = IsEquipped,
    };
}

/// <summary>
/// A tool that a Pingu can hold and use.
/// <remarks>Extends PinguAccessory for bone attachment and transform offsets.</remarks>
/// </summary>
public class PinguTool : PinguAccessory
{
    public PinguToolType ToolType { get; set; }
    public string UseAnimation { get; set; } = string.Empty;
    public string EquipAnimation { get; set; } = "equip";
    public string UnequipAnimation { get; set; } = "unequip";

    public new PinguTool Clone() => new()
    {
        Name = Name,
        ToolType = ToolType,
        AttachedBoneIndex = AttachedBoneIndex,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        OffsetZ = OffsetZ,
        Roll = Roll,
        Pitch = Pitch,
        Yaw = Yaw,
        Scale = Scale,
        Color = Color,
        UseAnimation = UseAnimation,
        EquipAnimation = EquipAnimation,
        UnequipAnimation = UnequipAnimation,
    };
}

// ============================================================================
// Mesh
// ============================================================================

/// <summary>
/// A single vertex in the Pingu mesh with position, normal, UV, and skinning data.
/// </summary>
public class PinguVertex
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float NormalX { get; set; }
    public float NormalY { get; set; }
    public float NormalZ { get; set; }
    public float U { get; set; }
    public float V { get; set; }
    public byte BoneIndex0 { get; set; }
    public byte BoneIndex1 { get; set; }
    public byte BoneIndex2 { get; set; }
    public byte BoneIndex3 { get; set; }
    public float BoneWeight0 { get; set; }
    public float BoneWeight1 { get; set; }
    public float BoneWeight2 { get; set; }
    public float BoneWeight3 { get; set; }
}

/// <summary>
/// A single triangle in the Pingu mesh, referencing three vertex indices.
/// </summary>
public class PinguTriangle
{
    public int Vertex0 { get; set; }
    public int Vertex1 { get; set; }
    public int Vertex2 { get; set; }
    public float ZOrder { get; set; }
}

/// <summary>
/// Complete mesh data for a Pingu character, including vertices, triangles, and texture atlas reference.
/// </summary>
public class PinguMeshData
{
    public List<PinguVertex> Vertices { get; set; } = new();
    public List<PinguTriangle> Triangles { get; set; } = new();
    public byte[]? TextureAtlasBytes { get; set; }
    public int AtlasWidth { get; set; } = 512;
    public int AtlasHeight { get; set; } = 512;
    public int VertexCount => Vertices.Count;
    public int TriangleCount => Triangles.Count;
    public string VertexCountDisplay => VertexCount >= 1000 ? $"{VertexCount / 1000.0:F1}K" : VertexCount.ToString();
    public string TriangleCountDisplay => TriangleCount >= 1000 ? $"{TriangleCount / 1000.0:F1}K" : TriangleCount.ToString();

    public PinguMeshData Clone()
    {
        return new PinguMeshData
        {
            Vertices = Vertices.Select(v => new PinguVertex
            {
                X = v.X, Y = v.Y, Z = v.Z,
                NormalX = v.NormalX, NormalY = v.NormalY, NormalZ = v.NormalZ,
                U = v.U, V = v.V,
                BoneIndex0 = v.BoneIndex0, BoneIndex1 = v.BoneIndex1,
                BoneIndex2 = v.BoneIndex2, BoneIndex3 = v.BoneIndex3,
                BoneWeight0 = v.BoneWeight0, BoneWeight1 = v.BoneWeight1,
                BoneWeight2 = v.BoneWeight2, BoneWeight3 = v.BoneWeight3,
            }).ToList(),
            Triangles = Triangles.Select(t => new PinguTriangle
            {
                Vertex0 = t.Vertex0, Vertex1 = t.Vertex1, Vertex2 = t.Vertex2,
                ZOrder = t.ZOrder,
            }).ToList(),
            TextureAtlasBytes = TextureAtlasBytes?.ToArray(),
            AtlasWidth = AtlasWidth,
            AtlasHeight = AtlasHeight,
        };
    }

    public void Clear() 
    { 
        Vertices.Clear(); 
        Triangles.Clear();
        AtlasWidth = 512;
        AtlasHeight = 512;
    }
}

// ============================================================================
// Bones
// ============================================================================

/// <summary>
/// A single bone in the skeletal hierarchy with transform and joint constraints.
/// </summary>
public class PinguBone
{
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }
    public int? ParentIndex { get; set; }

    private float _x, _y, _z, _roll, _pitch, _yaw, _scale = 1.0f;

    public float X { get => _x; set { _x = value; InvalidateMatrices(); } }
    public float Y { get => _y; set { _y = value; InvalidateMatrices(); } }
    public float Z { get => _z; set { _z = value; InvalidateMatrices(); } }
    public float Roll { get => _roll; set { _roll = value; InvalidateMatrices(); } }
    public float Pitch { get => _pitch; set { _pitch = value; InvalidateMatrices(); } }
    public float Yaw { get => _yaw; set { _yaw = value; InvalidateMatrices(); } }
    public float Scale { get => _scale; set { _scale = value; InvalidateMatrices(); } }

    public float MinRoll { get; set; } = -180f;
    public float MaxRoll { get; set; } = 180f;
    public float MinPitch { get; set; } = -180f;
    public float MaxPitch { get; set; } = 180f;
    public float MinYaw { get; set; } = -180f;
    public float MaxYaw { get; set; } = 180f;
    public PinguBone? Parent { get; set; }
    public List<PinguBone> Children { get; set; } = new();
    public float[] WorldMatrix { get; set; } = new float[16];
    public float[] LocalMatrix { get; set; } = new float[16];
    private bool _worldMatrixDirty = true;
    public bool IsRoot => ParentIndex == null;
    public int Depth { get; set; }
    private bool _localMatrixDirty = true;

    public void ComputeLocalMatrix()
    {
        var local = new float[16];
        var cp = (float)Math.Cos(Pitch * Math.PI / 180.0);
        var sp = (float)Math.Sin(Pitch * Math.PI / 180.0);
        var cy = (float)Math.Cos(Yaw * Math.PI / 180.0);
        var sy = (float)Math.Sin(Yaw * Math.PI / 180.0);
        var cr = (float)Math.Cos(Roll * Math.PI / 180.0);
        var sr = (float)Math.Sin(Roll * Math.PI / 180.0);

        local[0] = (cy * cr + sy * sp * sr) * Scale;
        local[1] = (sy * cp) * Scale;
        local[2] = (sy * sp * cr - cy * sr) * Scale;
        local[3] = X;
        local[4] = (-sy * cr + cy * sp * sr) * Scale;
        local[5] = (cy * cp) * Scale;
        local[6] = (cy * sp * cr + sy * sr) * Scale;
        local[7] = Y;
        local[8] = (cp * sr) * Scale;
        local[9] = (-sp) * Scale;
        local[10] = (cp * cr) * Scale;
        local[11] = Z;
        local[12] = 0f; local[13] = 0f; local[14] = 0f; local[15] = 1f;
        LocalMatrix = local;
        _localMatrixDirty = true;
    }

    public void ComputeWorldMatrix()
    {
        if (_worldMatrixDirty || _localMatrixDirty)
        {
            _worldMatrixDirty = false;
            _localMatrixDirty = false;
            if (Parent != null)
            {
                var parentWorld = Parent.WorldMatrix;
                var local = LocalMatrix;
                var world = new float[16];
                for (var i = 0; i < 4; i++)
                    for (var j = 0; j < 4; j++)
                        world[i + j * 4] = parentWorld[i + 0 * 4] * local[0 + j * 4] +
                                           parentWorld[i + 1 * 4] * local[1 + j * 4] +
                                           parentWorld[i + 2 * 4] * local[2 + j * 4] +
                                           parentWorld[i + 3 * 4] * local[3 + j * 4];
                WorldMatrix = world;
            }
            else
            {
                WorldMatrix = (float[])LocalMatrix.Clone();
            }
        }
    }

    public void InvalidateMatrices() { _worldMatrixDirty = true; _localMatrixDirty = true; }

    public PinguBone Clone() => new()
    {
        Name = Name, Index = Index, ParentIndex = ParentIndex,
        X = X, Y = Y, Z = Z,
        Roll = Roll, Pitch = Pitch, Yaw = Yaw, Scale = Scale,
        MinRoll = MinRoll, MaxRoll = MaxRoll,
        MinPitch = MinPitch, MaxPitch = MaxPitch,
        MinYaw = MinYaw, MaxYaw = MaxYaw, Depth = Depth,
    };
}

/// <summary>
/// A bone hierarchy definition loaded from JSON schema.
/// </summary>
public class PinguBoneHierarchy
{
    public List<PinguBoneDefinition> Definitions { get; set; } = new();
    public List<PinguBone> ResolvedBones { get; set; } = new();
    public List<PinguBone> RootBones { get; set; } = new();

    public static PinguBoneHierarchy CreateDefault()
    {
        var definitions = new List<PinguBoneDefinition>
        {
            new() { Name = "root", Index = 0, ParentIndex = null, X = 0, Y = 0, Z = 0 },
            new() { Name = "torso", Index = 1, ParentIndex = 0, X = 0, Y = 30, Z = 0 },
            new() { Name = "chest", Index = 2, ParentIndex = 1, X = 0, Y = 35, Z = 0 },
            new() { Name = "neck", Index = 3, ParentIndex = 2, X = 0, Y = -30, Z = 0 },
            new() { Name = "head", Index = 4, ParentIndex = 3, X = 0, Y = -25, Z = 0 },
            new() { Name = "leftArm", Index = 5, ParentIndex = 2, X = -30, Y = -10, Z = 0, MinRoll = -90, MaxRoll = 45 },
            new() { Name = "leftForeArm", Index = 6, ParentIndex = 5, X = -25, Y = 0, Z = 0 },
            new() { Name = "leftHand", Index = 7, ParentIndex = 6, X = -20, Y = 0, Z = 0 },
            new() { Name = "rightArm", Index = 8, ParentIndex = 2, X = 30, Y = -10, Z = 0, MinRoll = -45, MaxRoll = 90 },
            new() { Name = "rightForeArm", Index = 9, ParentIndex = 8, X = 25, Y = 0, Z = 0 },
            new() { Name = "rightHand", Index = 10, ParentIndex = 9, X = 20, Y = 0, Z = 0 },
            new() { Name = "leftUpLeg", Index = 11, ParentIndex = 0, X = -15, Y = 0, Z = 0 },
            new() { Name = "leftLeg", Index = 12, ParentIndex = 11, X = 0, Y = -35, Z = 0 },
            new() { Name = "leftFoot", Index = 13, ParentIndex = 12, X = 0, Y = -30, Z = 10 },
            new() { Name = "rightUpLeg", Index = 14, ParentIndex = 0, X = 15, Y = 0, Z = 0 },
            new() { Name = "rightLeg", Index = 15, ParentIndex = 14, X = 0, Y = -35, Z = 0 },
            new() { Name = "rightFoot", Index = 16, ParentIndex = 15, X = 0, Y = -30, Z = 10 },
            new() { Name = "leftEar", Index = 17, ParentIndex = 4, X = -15, Y = 20, Z = 10 },
            new() { Name = "rightEar", Index = 18, ParentIndex = 4, X = 15, Y = 20, Z = 10 },
        };
        var hierarchy = new PinguBoneHierarchy { Definitions = definitions };
        hierarchy.Resolve();
        return hierarchy;
    }

    public void Resolve()
    {
        ResolvedBones.Clear();
        RootBones.Clear();
        var nameToBone = new Dictionary<string, PinguBone>(Definitions.Count);
        foreach (var def in Definitions)
        {
            var bone = new PinguBone
            {
                Name = def.Name, Index = def.Index, ParentIndex = def.ParentIndex,
                X = def.X, Y = def.Y, Z = def.Z,
                Roll = def.Roll, Pitch = def.Pitch, Yaw = def.Yaw, Scale = def.Scale,
                MinRoll = def.MinRoll, MaxRoll = def.MaxRoll,
                MinPitch = def.MinPitch, MaxPitch = def.MaxPitch,
                MinYaw = def.MinYaw, MaxYaw = def.MaxYaw,
            };
            nameToBone[def.Name] = bone;
        }
        ResolvedBones = nameToBone.Values.ToList();
        foreach (var bone in ResolvedBones)
        {
            if (bone.ParentIndex.HasValue)
            {
                var parent = ResolvedBones.FirstOrDefault(b => b.Index == bone.ParentIndex);
                if (parent != null) { bone.Parent = parent; parent.Children.Add(bone); }
                else { bone.ParentIndex = null; }
            }
        }
        RootBones = ResolvedBones.Where(b => b.Parent == null).ToList();
        foreach (var root in RootBones) ComputeDepth(root, 0);
    }

    private void ComputeDepth(PinguBone bone, int depth)
    {
        bone.Depth = depth;
        foreach (var child in bone.Children) ComputeDepth(child, depth + 1);
    }

    public void InvalidateDepths() { foreach (var bone in ResolvedBones) bone.Depth = -1; }
    public int BoneCount => ResolvedBones.Count;
    public int RootBoneCount => RootBones.Count;
    public PinguBone? FindBoneByName(string name) => ResolvedBones.FirstOrDefault(b => b.Name == name);
    public PinguBone? FindBoneByIndex(int index) => ResolvedBones.FirstOrDefault(b => b.Index == index);
    public List<PinguBone> GetDescendants(PinguBone bone)
    {
        var descendants = new List<PinguBone>();
        CollectDescendants(bone, descendants);
        return descendants;
    }
    private void CollectDescendants(PinguBone bone, List<PinguBone> descendants)
    {
        foreach (var child in bone.Children) { descendants.Add(child); CollectDescendants(child, descendants); }
    }
}

/// <summary>
/// A bone definition from the JSON schema (before parent resolution).
/// </summary>
public class PinguBoneDefinition
{
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }
    public int? ParentIndex { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Scale { get; set; } = 1.0f;
    public float MinRoll { get; set; } = -180f;
    public float MaxRoll { get; set; } = 180f;
    public float MinPitch { get; set; } = -180f;
    public float MaxPitch { get; set; } = 180f;
    public float MinYaw { get; set; } = -180f;
    public float MaxYaw { get; set; } = 180f;
}

// ============================================================================
// Animation
// ============================================================================

/// <summary>
/// A single keyframe for a bone in an animation clip.
/// </summary>
public class AnimationKeyframe
{
    public float Time { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Scale { get; set; } = 1.0f;
}

/// <summary>
/// Animation keyframes for a single bone.
/// </summary>
public class BoneAnimationTrack
{
    public int BoneIndex { get; set; }
    public List<AnimationKeyframe> Keyframes { get; set; } = new();
}

/// <summary>
/// A complete animation clip for a character.
/// </summary>
public class PinguAnimationClip
{
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; }
    public bool Looping { get; set; } = true;
    public List<BoneAnimationTrack> Tracks { get; set; } = new();
    public int TotalKeyframes => Tracks.Sum(t => t.Keyframes.Count);

    private int? _cachedMaxTrackKeyframes;
    private bool _maxTrackKeyframesDirty = true;

    public int MaxTrackKeyframes
    {
        get
        {
            if (_maxTrackKeyframesDirty)
            {
                _cachedMaxTrackKeyframes = Tracks.Count > 0 ? Tracks.Max(t => t.Keyframes.Count) : 0;
                _maxTrackKeyframesDirty = false;
            }
            return _cachedMaxTrackKeyframes ?? 0;
        }
        internal set { _cachedMaxTrackKeyframes = value; _maxTrackKeyframesDirty = false; }
    }

    public int KeyframeCount => MaxTrackKeyframes;

    public void SortKeyframes() { foreach (var track in Tracks) track.Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time)); }

    public static List<PinguAnimationClip> CreateDefaultClips()
    {
        var defaultBoneNames = new[] { "root", "torso", "chest", "neck", "head", "leftArm", "leftForeArm", "leftHand", "rightArm", "rightForeArm", "rightHand", "leftUpLeg", "leftLeg", "leftFoot", "rightUpLeg", "rightLeg", "rightFoot", "leftEar", "rightEar" };
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
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateWalkTracks(string[] boneNames, Random random)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
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
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateSitTracks(string[] boneNames)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
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
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateWaveTracks(string[] boneNames)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
        {
            var track = new BoneAnimationTrack { BoneIndex = Array.IndexOf(boneNames, bone) };
            if (bone == "rightArm" || bone == "rightForeArm")
                track.Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.25f, Roll = -45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 0.5f, Roll = 45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 0.75f, Roll = -45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 45, Pitch = 10, Yaw = 0, Scale = 1f },
                };
            else
                track.Keyframes = new List<AnimationKeyframe>
                {
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                };
            tracks.Add(track);
        }
        return tracks;
    }

    private static List<BoneAnimationTrack> CreateTwitchTracks(string[] boneNames, Random random)
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in boneNames)
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
        return tracks;
    }
}

/// <summary>
/// Configuration for a specific animation state.
/// </summary>
public class PinguAnimationStateConfig
{
    public PinguAnimationState State { get; set; }
    public string? AnimationClipName { get; set; }
    public float DurationMin { get; set; } = 1.0f;
    public float DurationMax { get; set; } = 3.0f;
    public float BlendSpeed { get; set; } = 0.15f;
    public bool Interruptible { get; set; } = true;
    public int Priority { get; set; } = 0;

    public bool IsValid() => DurationMin > 0f && DurationMin <= DurationMax && DurationMax > 0f && BlendSpeed > 0f && AnimationClipName != null;
}

// ============================================================================
// Physics
// ============================================================================

/// <summary>
/// Physics parameters for a Pingu character.
/// </summary>
public class PinguPhysicsParams
{
    public float Mass { get; set; } = 1.0f;
    public float Friction { get; set; } = 0.6f;
    public float Gravity { get; set; } = 980.0f;
    public float IkStiffness { get; set; } = 0.7f;
    public float VelocityDamping { get; set; } = 0.95f;
    public float SpringStiffness { get; set; } = 0.1f;
    public float SpringRestLength { get; set; } = 1.0f;
    public bool AffectedByGravity { get; set; } = true;
    public float MaxWalkSpeed { get; set; } = 100.0f;
    public float MaxRunSpeed { get; set; } = 200.0f;
    public float Acceleration { get; set; } = 300.0f;
    public float Deceleration { get; set; } = 400.0f;

    public bool IsValid()
    {
        return Mass > 0f && Friction >= 0f && Friction <= 1f && Gravity >= 0f &&
               IkStiffness >= 0f && IkStiffness <= 1f && VelocityDamping >= 0f && VelocityDamping <= 1f &&
               SpringStiffness >= 0f && SpringRestLength >= 0f && MaxWalkSpeed > 0f && MaxRunSpeed > 0f &&
               Acceleration >= 0f && Deceleration >= 0f;
    }

    public PinguPhysicsParams Clone() => new()
    {
        Mass = Mass, Friction = Friction, Gravity = Gravity, IkStiffness = IkStiffness,
        VelocityDamping = VelocityDamping, SpringStiffness = SpringStiffness,
        SpringRestLength = SpringRestLength, AffectedByGravity = AffectedByGravity,
        MaxWalkSpeed = MaxWalkSpeed, MaxRunSpeed = MaxRunSpeed,
        Acceleration = Acceleration, Deceleration = Deceleration,
    };

    public bool HasConsistentSpeedLimits() => MaxWalkSpeed <= MaxRunSpeed;
}

// ============================================================================
// NPC
// ============================================================================

/// <summary>
/// A pending task in the NPC queue.
/// </summary>
public class PinguTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public PinguTaskType Type { get; set; }
    public int Priority { get; set; } = 0;
    public PinguTaskStatus Status { get; set; } = PinguTaskStatus.Pending;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
}

/// <summary>
/// A Pingu NPC character with role, tool, and task state.
/// </summary>
public class PinguNPC
{
    public string Name { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string BodyColor { get; set; } = "#FFFFFF";
    public string BellyColor { get; set; } = "#F0F0F0";
    public string BeakColor { get; set; } = "#FFA500";
    public float HeightScale { get; set; } = 1.0f;
    public float WidthScale { get; set; } = 1.0f;
    public float X { get; set; }
    public float Y { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float Rotation { get; set; }
    public float TargetRotation { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public bool IsMoving { get; set; }
    public float MoveProgress { get; set; }
    public float MoveDuration { get; set; }
    public PinguRole CurrentRole { get; set; } = PinguRole.Idle;
    public PinguAnimationState CurrentAnimation { get; set; } = PinguAnimationState.Idle;
    public PinguNPCState State { get; set; } = PinguNPCState.Idle;
    public List<PinguTask> TaskQueue { get; set; } = new();
    public PinguTask? CurrentTask { get; set; }
    public PinguHat? Hat { get; set; }
    public PinguTool? Tool { get; set; }
    public PinguPhysicsParams Physics { get; set; } = new();
    public PinguToolType CurrentToolType => Tool?.ToolType ?? PinguToolType.None;
    public bool HasReachedTarget => !IsMoving && Math.Abs(X - TargetX) < 0.5f && Math.Abs(Y - TargetY) < 0.5f;

    private float _moveStartX = 0f;
    private float _moveStartY = 0f;

    public void MoveTo(float targetX, float targetY, float duration)
    {
        _moveStartX = X; _moveStartY = Y;
        TargetX = targetX; TargetY = targetY;
        MoveDuration = duration; MoveProgress = 0f; IsMoving = true;
        var dx = targetX - _moveStartX;
        var dy = targetY - _moveStartY;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        VelocityX = distance > 0 ? dx / distance : 0f;
        VelocityY = distance > 0 ? dy / distance : 0f;
    }

    public void UpdatePosition(float deltaTime = 1f / 60f)
    {
        if (!IsMoving) return;
        MoveProgress += deltaTime / MoveDuration;
        if (MoveProgress >= 1.0f)
        {
            X = TargetX; Y = TargetY;
            VelocityX = 0f; VelocityY = 0f;
            IsMoving = false; MoveProgress = 1f;
        }
        else
        {
            var t = MoveProgress;
            var easedT = t < 0.5f ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2f;
            var dx = TargetX - _moveStartX;
            var dy = TargetY - _moveStartY;
            X = _moveStartX + dx * (float)easedT;
            Y = _moveStartY + dy * (float)easedT;
            VelocityX = dx / MoveDuration;
            VelocityY = dy / MoveDuration;
        }
    }
}

// ============================================================================
// Home Scene
// ============================================================================

/// <summary>
/// A static object in the Pingu home scene.
/// </summary>
public class PinguHomeObject
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string Color { get; set; } = "#808080";
    public bool IsInteractive { get; set; } = false;
    public string? InteractionAnimation { get; set; }
    public bool IsVisible { get; set; } = true;

    public PinguHomeObject Clone() => new()
    {
        Name = Name, Type = Type, X = X, Y = Y, Z = Z,
        Width = Width, Height = Height, Color = Color,
        IsInteractive = IsInteractive, InteractionAnimation = InteractionAnimation,
        IsVisible = IsVisible,
    };

    public static PinguHomeObject CreateDefaultIgloo() => new()
    { Name = "Igloo", Type = "igloo", X = 0.15f, Y = 0.2f, Z = 0.2f, Width = 80, Height = 90, Color = "#E8F4F8", IsInteractive = true, InteractionAnimation = "sit" };
    public static PinguHomeObject CreateDefaultSink() => new()
    { Name = "Sink", Type = "sink", X = 0.6f, Y = 0.65f, Z = 0.3f, Width = 60, Height = 40, Color = "#B0C4DE", IsInteractive = true, InteractionAnimation = "wash" };
    public static PinguHomeObject CreateDefaultRug() => new()
    { Name = "Rug", Type = "rug", X = 0.3f, Y = 0.75f, Z = 0.05f, Width = 120, Height = 30, Color = "#8B4513", IsInteractive = false };
    public static PinguHomeObject CreateDefaultBall() => new()
    { Name = "Ball", Type = "ball", X = 0.75f, Y = 0.55f, Z = 0.15f, Width = 30, Height = 30, Color = "#FF4500", IsInteractive = true, InteractionAnimation = "play" };
    public static PinguHomeObject CreateDefaultFishBowl() => new()
    { Name = "Fish Bowl", Type = "fishbowl", X = 0.45f, Y = 0.15f, Z = 0.4f, Width = 50, Height = 50, Color = "#87CEEB", IsInteractive = true, InteractionAnimation = "watch" };
    public static PinguHomeObject CreateDefaultNest() => new()
    { Name = "Nest", Type = "nest", X = 0.05f, Y = 0.6f, Z = 0.1f, Width = 70, Height = 50, Color = "#D2691E", IsInteractive = true, InteractionAnimation = "sleep" };
}

/// <summary>
/// The Pingu home scene with decorative objects.
/// </summary>
public class PinguHomeScene
{
    public string Name { get; set; } = "Default Home";
    public string BackgroundColor { get; set; } = "#1E1E22";
    public float Width { get; set; } = 200f;
    public float Height { get; set; } = 200f;
    public float Depth { get; set; } = 100f;
    public float X { get; set; } = 0f;
    public float Y { get; set; } = 0f;
    public string? BoneHierarchyJson { get; set; }
    public List<PinguHomeObject> Objects { get; set; } = new();

    private List<PinguHomeObject>? _sortedObjects;
    public IReadOnlyList<PinguHomeObject> SortedObjects
    {
        get
        {
            if (_sortedObjects == null) _sortedObjects = Objects.OrderBy(o => o.Z).ToList();
            return _sortedObjects;
        }
    }
    public void InvalidateSortedObjects() => _sortedObjects = null;

    public static PinguHomeScene CreateDefault() => new()
    {
        Name = "Default Home", BackgroundColor = "#1E1E22",
        Width = 200f, Height = 200f, Depth = 100f,
        Objects = new List<PinguHomeObject>
        {
            PinguHomeObject.CreateDefaultIgloo(), PinguHomeObject.CreateDefaultSink(),
            PinguHomeObject.CreateDefaultRug(), PinguHomeObject.CreateDefaultBall(),
            PinguHomeObject.CreateDefaultFishBowl(), PinguHomeObject.CreateDefaultNest(),
        },
    };
}

// ============================================================================
// Character Data
// ============================================================================

/// <summary>
/// Complete character data for a Pingu, combining mesh, bones, animations, and physics.
/// </summary>
public class PinguCharacterData
{
    public PinguMeshData MeshData { get; set; } = new();
    public PinguBoneHierarchy BoneHierarchy { get; set; } = new();
    public List<PinguAnimationClip> AnimationClips { get; set; } = new();
    public PinguPhysicsParams PhysicsParams { get; set; } = new();
    public byte[]? TextureAtlas { get; set; }
    public int? Seed { get; set; }

    private string? _boneHierarchyJson;
    public string BoneHierarchyJson
    {
        get
        {
            if (_boneHierarchyJson == null)
                _boneHierarchyJson = System.Text.Json.JsonSerializer.Serialize(BoneHierarchy.Definitions, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            return _boneHierarchyJson;
        }
    }
    public void InvalidateBoneHierarchyJson() => _boneHierarchyJson = null;
}

// ============================================================================
// State
// ============================================================================

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
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;
    public float BehaviorFrequencyMultiplier { get; set; } = 1.0f;
    public float PinguSizeMultiplier { get; set; } = 1.0f;
    public bool IsAwake { get; set; }
    public bool HasGguf { get; set; }
    public bool HasLlamaCpp { get; set; }
    public AwakeningPhase AwakeningPhase { get; set; } = AwakeningPhase.None;
    public bool IsLoadingModel { get; set; }
    public double LoadProgress { get; set; }

    public void Reset()
    {
        Mood = PinguMood.Idle; IsVisible = true; IsMenuOpen = false;
        ActivePanel = PinguPanelType.None; IsBlinking = false; MouthFrame = 0;
        BobSpeed = 1.0; BlinkIntervalMin = 1.5; BlinkIntervalMax = 3.0;
        AwakeningPhase = AwakeningPhase.None; IsLoadingModel = false; LoadProgress = 0;
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

// ============================================================
// PinguAutomation.cs (67 lines)
// ============================================================

/// <summary>
/// Pingu automation providing action animations and drag-to-pause VM management.
/// </summary>
public class PinguAutomation
{
    private readonly IPinguStore _pingu;
    private readonly IQEMUProcessManager _qemuManager;

    public PinguAutomation(IPinguStore pingu, IQEMUProcessManager qemuManager)
    {
        _pingu = pingu;
        _qemuManager = qemuManager;
    }

    public async Task EnterControlModeAsync()
    {
        await _pingu.UpdateMoodAsync(PinguMood.Working);
    }

    public async Task HandleDragToPauseAsync(DragEvent e)
    {
        if (e?.Target == "pingu")
        {
            foreach (var vm in _qemuManager.Instances.Where(v => v.State == VMRunState.Running))
            {
                await _qemuManager.PauseVMAsync(vm.Id).ConfigureAwait(false);
            }
            await _pingu.UpdateMoodAsync(PinguMood.Idle).ConfigureAwait(false);
        }
    }

    public async Task PerformActionAsync(string action, Element? target = null)
    {
        if (target == null)
        {
            await _pingu.UpdateMoodAsync(PinguMood.Happy).ConfigureAwait(false);
            return;
        }

        switch (action)
        {
            case "startVM":
                if (target.VmId != null)
                {
                    await _qemuManager.StartVMAsync(target.VmId).ConfigureAwait(false);
                    await _pingu.UpdateMoodAsync(PinguMood.Happy).ConfigureAwait(false);
                }
                break;
            case "stopVM":
                if (target.VmId != null)
                {
                    await _qemuManager.StopVMAsync(target.VmId).ConfigureAwait(false);
                    await _pingu.UpdateMoodAsync(PinguMood.Idle).ConfigureAwait(false);
                }
                break;
            default:
                await _pingu.UpdateMoodAsync(PinguMood.Happy).ConfigureAwait(false);
                break;
        }
    }
}

// ============================================================
// ModelLifecycleTracer.cs (115 lines)
// ============================================================

/// <summary>
/// No-op ILogger for when DI is not configured.
/// </summary>
internal class NoOpLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

/// <summary>
/// Traces model lifecycle events including load/unload times and VRAM allocation changes.
/// Tracks per-model timing and resource consumption.
/// </summary>
public class ModelLifecycleTracer : IDisposable
{
    private readonly ILogger<ModelLifecycleTracer>? _logger;
    private readonly ConcurrentDictionary<string, ModelLoadTrace> _activeLoads = new();
    private readonly ConcurrentQueue<ModelLoadTrace> _recentTraces = new();
    private bool _disposed;

    public ModelLifecycleTracer(ILogger<ModelLifecycleTracer>? logger = null)
    {
        _logger = logger ?? new NoOpLogger<ModelLifecycleTracer>();
    }

    public int ActiveLoadCount => _activeLoads.Count;
    public int TracedModels => _recentTraces.Count;

    public void TrackModelLoad(string modelId, string modelType, long fileSizeBytes, [CallerMemberName] string caller = "")
    {
        if (_disposed) return;

        var trace = new ModelLoadTrace
        {
            ModelId = modelId,
            ModelType = modelType,
            FileSizeBytes = fileSizeBytes,
            StartedAt = DateTime.UtcNow,
            Caller = caller
        };

        _activeLoads[modelId] = trace;
        _logger?.LogDebug("[Lifecycle] Load started: {ModelId} ({ModelType}) from {Caller}", modelId, modelType, caller);
    }

    public void CompleteModelLoad(string modelId, float loadTimeMs, long vramAllocated, bool success)
    {
        if (_disposed) return;

        if (!_activeLoads.TryRemove(modelId, out var trace)) return;

        trace.CompletedAt = DateTime.UtcNow;
        trace.LoadTimeMs = loadTimeMs;
        trace.VramAllocatedBytes = vramAllocated;
        trace.Success = success;

        _recentTraces.Enqueue(trace);

        // Keep only last 100 traces
        while (_recentTraces.Count > 100)
        {
            _recentTraces.TryDequeue(out _);
        }

        if (success)
        {
            _logger?.LogInformation("[Lifecycle] Load complete: {ModelId} in {Time:F1}ms (VRAM: {Vram:N0} bytes)",
                modelId, loadTimeMs, vramAllocated);
        }
        else
        {
            _logger?.LogWarning("[Lifecycle] Load failed: {ModelId} ({Time:F1}ms)", modelId, loadTimeMs);
        }
    }

    public void TrackModelUnload(string modelId)
    {
        if (_disposed) return;
        _logger?.LogDebug("[Lifecycle] Unload started: {ModelId}", modelId);
    }

    public IReadOnlyList<ModelLoadTrace> GetRecentTraces(int count = 20)
    {
        return _recentTraces.Take(count).ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    public record ModelLoadTrace
    {
        public string ModelId { get; set; } = "";
        public string ModelType { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public float? LoadTimeMs { get; set; }
        public long? VramAllocatedBytes { get; set; }
        public bool? Success { get; set; }
        public string Caller { get; set; } = "";
    }
}
