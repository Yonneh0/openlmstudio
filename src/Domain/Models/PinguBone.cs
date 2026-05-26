namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A single bone in the skeletal hierarchy with transform and joint constraints.
/// </summary>
public class PinguBone
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

    // Joint limits
    public float MinRoll { get; set; } = -180f;
    public float MaxRoll { get; set; } = 180f;
    public float MinPitch { get; set; } = -180f;
    public float MaxPitch { get; set; } = 180f;
    public float MinYaw { get; set; } = -180f;
    public float MaxYaw { get; set; } = 180f;

    /// <summary>
    /// The parent bone (resolved after loading).
    /// </summary>
    public PinguBone? Parent { get; set; }

    /// <summary>
    /// Child bones (resolved after loading).
    /// </summary>
    public List<PinguBone> Children { get; set; } = new();

    /// <summary>
    /// Computed world transform matrix.
    /// </summary>
    public float[] WorldMatrix { get; set; } = new float[16];

    /// <summary>
    /// Computed local transform matrix.
    /// </summary>
    public float[] LocalMatrix { get; set; } = new float[16];

    /// <summary>
    /// Whether this bone is a root bone (no parent).
    /// </summary>
    public bool IsRoot => ParentIndex == null;

    /// <summary>
    /// Depth in the bone hierarchy.
    /// </summary>
    public int Depth { get; set; }
}

/// <summary>
/// A bone hierarchy definition loaded from JSON schema.
/// </summary>
public class PinguBoneHierarchy
{
    public List<PinguBoneDefinition> Definitions { get; set; } = new();
    public List<PinguBone> ResolvedBones { get; set; } = new();
    public List<PinguBone> RootBones { get; set; } = new();

    /// <summary>
    /// Resolve parent-child relationships from flat definitions.
    /// </summary>
    public void Resolve()
    {
        var nameToBone = Definitions.ToDictionary(d => d.Name, d => new PinguBone
        {
            Name = d.Name,
            Index = d.Index,
            ParentIndex = d.ParentIndex,
            X = d.X,
            Y = d.Y,
            Z = d.Z,
            Roll = d.Roll,
            Pitch = d.Pitch,
            Yaw = d.Yaw,
            Scale = d.Scale,
            MinRoll = d.MinRoll,
            MaxRoll = d.MaxRoll,
            MinPitch = d.MinPitch,
            MaxPitch = d.MaxPitch,
            MinYaw = d.MinYaw,
            MaxYaw = d.MaxYaw,
        });

        ResolvedBones = nameToBone.Values.ToList();
        RootBones = ResolvedBones.Where(b => b.ParentIndex == null).ToList();

        foreach (var bone in ResolvedBones)
        {
            if (bone.ParentIndex.HasValue)
            {
                var parent = ResolvedBones.FirstOrDefault(b => b.Index == bone.ParentIndex);
                if (parent != null)
                {
                    bone.Parent = parent;
                    parent.Children.Add(bone);
                }
            }
        }

        // Compute depth
        foreach (var root in RootBones)
            ComputeDepth(root, 0);
    }

    private void ComputeDepth(PinguBone bone, int depth)
    {
        bone.Depth = depth;
        foreach (var child in bone.Children)
            ComputeDepth(child, depth + 1);
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