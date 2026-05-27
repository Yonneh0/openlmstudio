using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// CCD (Cyclic Coordinate Descent) IK solver for Pingu limbs.
/// Iteratively adjusts bone rotations to move end effector toward target.
/// </summary>
public class PinguInverseKinematics
{
    private readonly ILogger<PinguInverseKinematics>? _logger;
    private readonly Random _random;
    private const int DefaultIterations = 10;
    private const float DefaultConvergenceThreshold = 0.01f;

    /// <summary>
    /// IK target: which bone is the end effector, and where is the target position?
    /// </summary>
    public record struct IKTarget(int BoneIndex, Vector3 TargetPosition, float Stiffness);

    private readonly List<IKTarget> _targets = new();

    /// <summary>
    /// Current bone rotations (Euler angles in radians).
    /// </summary>
    public float[] BoneRotations { get; private set; }

    /// <summary>
    /// Current bone positions (world space, flattened as [x0,y0,z0, x1,y1,z1, ...]).
    /// </summary>
    public float[] BonePositions { get; private set; }

    /// <summary>
    /// Bone parent hierarchy for IK chain traversal.
    /// </summary>
    private readonly int[] _boneParents;

    public PinguInverseKinematics(
        int boneCount,
        IReadOnlyList<int> boneParents,
        Vector3[] initialPositions,
        float[] initialRotations,
        ILogger<PinguInverseKinematics>? logger = null,
        Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        BoneRotations = initialRotations.ToArray();
        BonePositions = new float[boneCount * 3];
        for (var i = 0; i < boneCount; i++)
        {
            BonePositions[i * 3] = initialPositions[i].X;
            BonePositions[i * 3 + 1] = initialPositions[i].Y;
            BonePositions[i * 3 + 2] = initialPositions[i].Z;
        }
        _boneParents = boneParents.ToArray();
    }

    /// <summary>
    /// Set an IK target for a specific bone.
    /// </summary>
    public void SetTarget(int boneIndex, Vector3 targetPosition, float stiffness = 1.0f)
    {
        var existing = _targets.Find(t => t.BoneIndex == boneIndex);
        if (existing.BoneIndex >= 0)
        {
            _targets.Remove(existing);
        }
        _targets.Add(new IKTarget(boneIndex, targetPosition, stiffness));
    }

    /// <summary>
    /// Clear all IK targets.
    /// </summary>
    public void ClearTargets()
    {
        _targets.Clear();
    }

    /// <summary>
    /// Clear IK target for a specific bone.
    /// </summary>
    public void ClearTarget(int boneIndex)
    {
        _targets.RemoveAll(t => t.BoneIndex == boneIndex);
    }

    /// <summary>
    /// Solve IK for all targets using CCD.
    /// Returns the final error distances after solving.
    /// </summary>
    public IReadOnlyList<float> Solve(int iterations = DefaultIterations)
    {
        var errors = new List<float>();

        for (var iter = 0; iter < iterations; iter++)
        {
            foreach (var target in _targets)
            {
                var chain = GetBoneChain(target.BoneIndex);
                var solved = SolveChain(chain, target.TargetPosition, target.Stiffness);
                errors.Add(solved);
            }
        }

        return errors;
    }

    /// <summary>
    /// Update bone positions based on current rotations.
    /// </summary>
    public void UpdatePositions(PinguBoneHierarchy boneHierarchy)
    {
        var bones = boneHierarchy.ResolvedBones;
        BonePositions[0] = bones[0].X;
        BonePositions[1] = bones[0].Y;
        BonePositions[2] = bones[0].Z;

        for (var i = 1; i < bones.Count; i++)
        {
            var parentIdx = _boneParents[i];
            if (parentIdx < 0 || parentIdx >= bones.Count)
                continue;

            var parent = bones[parentIdx];
            var bone = bones[i];

            // Apply rotation to bone offset
            var offset = new Vector3(bone.X - parent.X, bone.Y - parent.Y, bone.Z - parent.Z);
            var cosYaw = (float)Math.Cos(BoneRotations[i]);
            var sinYaw = (float)Math.Sin(BoneRotations[i]);
            var cosPitch = (float)Math.Cos(BoneRotations[i + 1]);
            var sinPitch = (float)Math.Sin(BoneRotations[i + 1]);

            // Apply yaw rotation
            var x = offset.X;
            var z = offset.Z;
            offset.X = x * cosYaw - z * sinYaw;
            offset.Z = x * sinYaw + z * cosYaw;

            // Apply pitch rotation
            var y = offset.Y;
            offset.Y = y * cosPitch - offset.Z * sinPitch;
            offset.Z = y * sinPitch + offset.Z * cosPitch;

            BonePositions[i * 3] = parent.X + offset.X;
            BonePositions[i * 3 + 1] = parent.Y + offset.Y;
            BonePositions[i * 3 + 2] = parent.Z + offset.Z;
        }
    }

    /// <summary>
    /// Get the chain of bones from root to the specified bone.
    /// </summary>
    private List<int> GetBoneChain(int boneIndex)
    {
        var chain = new List<int>();
        var current = boneIndex;
        while (current >= 0)
        {
            chain.Add(current);
            current = _boneParents[current];
        }
        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// Get the position of a specific bone.
    /// </summary>
    public System.Numerics.Vector3 GetBonePosition(int boneIndex)
    {
        return new System.Numerics.Vector3(BonePositions[boneIndex * 3], BonePositions[boneIndex * 3 + 1], BonePositions[boneIndex * 3 + 2]);
    }

    /// <summary>
    /// Solve a single bone chain toward the target position.
    /// </summary>
    private float SolveChain(List<int> chain, Vector3 target, float stiffness)
    {
        // Start from the end effector
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var boneIdx = chain[i];
            var bonePos = GetBonePosition(boneIdx);
            var toTarget = target - bonePos;
            var toChild = i > 0
                ? GetBonePosition(chain[i - 1]) - bonePos
                : target - bonePos;

            // Calculate rotation needed
            var angle = (float)Math.Atan2(
                toTarget.X * toChild.Z - toTarget.Z * toChild.X,
                toTarget.X * toChild.X + toTarget.Z * toChild.Z);

            BoneRotations[boneIdx] += angle * stiffness;
            target = target - RotateAroundBone(bonePos, angle);
        }

        return Vector3.Distance(GetBonePosition(chain[^1]), target);
    }

    /// <summary>
    /// Rotate a point around a bone axis.
    /// </summary>
    private Vector3 RotateAroundBone(Vector3 pivot, float angle)
    {
        var cos = (float)Math.Cos(angle);
        var sin = (float)Math.Sin(angle);
        return new Vector3(
            pivot.X * cos - pivot.Z * sin,
            pivot.Y,
            pivot.X * sin + pivot.Z * cos);
    }
}