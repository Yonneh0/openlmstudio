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
    /// Computed world transform matrix (column-major 4x4).
    /// </summary>
    public float[] WorldMatrix { get; set; } = new float[16];

    /// <summary>
    /// Computed local transform matrix (column-major 4x4).
    /// </summary>
    public float[] LocalMatrix { get; set; } = new float[16];

    /// <summary>
    /// Recomputes the local matrix from this bone's transform using proper ZYX Euler angle composition.
    /// </summary>
    public void ComputeLocalMatrix()
    {
        var local = new float[16];

        // Pre-compute trigonometric values
        var cp = (float)Math.Cos(Pitch * Math.PI / 180.0);
        var sp = (float)Math.Sin(Pitch * Math.PI / 180.0);
        var cy = (float)Math.Cos(Yaw * Math.PI / 180.0);
        var sy = (float)Math.Sin(Yaw * Math.PI / 180.0);
        var cr = (float)Math.Cos(Roll * Math.PI / 180.0);
        var sr = (float)Math.Sin(Roll * Math.PI / 180.0);

        // ZYX Euler angle composition (Roll=Z, Pitch=Y, Yaw=X) with scale
        // Column-major 4x4 matrix
        local[0] = (cy * cr + sy * sp * sr) * Scale;  // X column, row 0
        local[1] = (sy * cp) * Scale;                   // X column, row 1
        local[2] = (sy * sp * cr - cy * sr) * Scale;   // X column, row 2
        local[3] = X;

        local[4] = (-sy * cr + cy * sp * sr) * Scale;  // Y column, row 0
        local[5] = (cy * cp) * Scale;                   // Y column, row 1
        local[6] = (cy * sp * cr + sy * sr) * Scale;   // Y column, row 2
        local[7] = Y;

        local[8] = (cp * sr) * Scale;                   // Z column, row 0
        local[9] = (-sp) * Scale;                       // Z column, row 1
        local[10] = (cp * cr) * Scale;                 // Z column, row 2
        local[11] = Z;

        local[12] = 0f;
        local[13] = 0f;
        local[14] = 0f;
        local[15] = 1f;

        LocalMatrix = local;
    }

    /// <summary>
    /// Recomputes the world matrix by multiplying parent's world matrix by this bone's local matrix (column-major order).
    /// </summary>
    public void ComputeWorldMatrix()
    {
        if (Parent != null)
        {
            var parentWorld = Parent.WorldMatrix;
            var local = LocalMatrix;
            var world = new float[16];

            // Column-major matrix multiplication: world = parentWorld × local
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 4; j++)
                {
                    world[i + j * 4] = parentWorld[i + 0 * 4] * local[0 + j * 4] +
                                       parentWorld[i + 1 * 4] * local[1 + j * 4] +
                                       parentWorld[i + 2 * 4] * local[2 + j * 4] +
                                       parentWorld[i + 3 * 4] * local[3 + j * 4];
                }
            }

            WorldMatrix = world;
        }
        else
        {
            WorldMatrix = (float[])LocalMatrix.Clone();
        }
    }

    /// <summary>
    /// Whether this bone is a root bone (no parent).
    /// </summary>
    public bool IsRoot => ParentIndex == null;

    /// <summary>
    /// Depth in the bone hierarchy.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Creates a shallow copy of this bone (copies scalar properties but not Parent/Children references).
    /// </summary>
    public PinguBone Clone() => new()
    {
        Name = Name,
        Index = Index,
        ParentIndex = ParentIndex,
        X = X,
        Y = Y,
        Z = Z,
        Roll = Roll,
        Pitch = Pitch,
        Yaw = Yaw,
        Scale = Scale,
        MinRoll = MinRoll,
        MaxRoll = MaxRoll,
        MinPitch = MinPitch,
        MaxPitch = MaxPitch,
        MinYaw = MinYaw,
        MaxYaw = MaxYaw,
        Depth = Depth,
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

    /// <summary>
    /// Resolve parent-child relationships from flat definitions.
    /// </summary>
    public void Resolve()
    {
        // Clear previous state (but not Definitions, which is the source of truth)
        ResolvedBones.Clear();
        RootBones.Clear();

        // Single pass: create all bones from definitions
        var nameToBone = new Dictionary<string, PinguBone>(Definitions.Count);
        foreach (var def in Definitions)
        {
            var bone = new PinguBone
            {
                Name = def.Name,
                Index = def.Index,
                ParentIndex = def.ParentIndex,
                X = def.X,
                Y = def.Y,
                Z = def.Z,
                Roll = def.Roll,
                Pitch = def.Pitch,
                Yaw = def.Yaw,
                Scale = def.Scale,
                MinRoll = def.MinRoll,
                MaxRoll = def.MaxRoll,
                MinPitch = def.MinPitch,
                MaxPitch = def.MaxPitch,
                MinYaw = def.MinYaw,
                MaxYaw = def.MaxYaw,
            };
            nameToBone[def.Name] = bone;
        }

        ResolvedBones = nameToBone.Values.ToList();

        // Second pass: link parent-child relationships
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
                else
                {
                    // ParentIndex references a bone that doesn't exist — treat as root
                    bone.ParentIndex = null;
                }
            }
        }

        RootBones = ResolvedBones.Where(b => b.Parent == null).ToList();

        // Compute depth from roots
        foreach (var root in RootBones)
            ComputeDepth(root, 0);
    }

    private void ComputeDepth(PinguBone bone, int depth)
    {
        bone.Depth = depth;
        foreach (var child in bone.Children)
            ComputeDepth(child, depth + 1);
    }

    /// <summary>
    /// Marks all bone depths as stale (use after modifying hierarchy manually).
    /// </summary>
    public void InvalidateDepths()
    {
        foreach (var bone in ResolvedBones)
            bone.Depth = -1;
    }

    /// <summary>
    /// Gets the total number of bones in the hierarchy (resolved).
    /// </summary>
    public int BoneCount => ResolvedBones.Count;

    /// <summary>
    /// Gets the total number of root bones.
    /// </summary>
    public int RootBoneCount => RootBones.Count;

    /// <summary>
    /// Finds a bone by its name.
    /// </summary>
    public PinguBone? FindBoneByName(string name) => ResolvedBones.FirstOrDefault(b => b.Name == name);

    /// <summary>
    /// Finds a bone by its index.
    /// </summary>
    public PinguBone? FindBoneByIndex(int index) => ResolvedBones.FirstOrDefault(b => b.Index == index);

    /// <summary>
    /// Recursively collects all descendants of a bone.
    /// </summary>
    public List<PinguBone> GetDescendants(PinguBone bone)
    {
        var descendants = new List<PinguBone>();
        CollectDescendants(bone, descendants);
        return descendants;
    }

    private void CollectDescendants(PinguBone bone, List<PinguBone> descendants)
    {
        foreach (var child in bone.Children)
        {
            descendants.Add(child);
            CollectDescendants(child, descendants);
        }
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