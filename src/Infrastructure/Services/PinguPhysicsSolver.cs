using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Physics solver for Pingu characters with distance constraints, velocity damping, and gravity.
/// Synchronizes with the render loop for stable 60fps performance.
/// </summary>
public class PinguPhysicsSolver
{
    private readonly ILogger<PinguPhysicsSolver>? _logger;
    private readonly Random _random;
    private const float DefaultGravity = -9.81f;
    private const float DefaultDamping = 0.95f;
    private const float DefaultSpringStiffness = 10.0f;
    private const float DefaultRestLength = 1.0f;

    /// <summary>
    /// Distance constraint between two bones.
    /// </summary>
    public record struct DistanceConstraint(int BoneA, int BoneB, float RestLength, float Stiffness);

    /// <summary>
    /// Velocity for each bone.
    /// </summary>
    public Vector3[] BoneVelocities { get; private set; }

    /// <summary>
    /// Current gravity vector.
    /// </summary>
    public float Gravity { get; set; } = DefaultGravity;

    /// <summary>
    /// Global velocity damping factor.
    /// </summary>
    public float Damping { get; set; } = DefaultDamping;

    /// <summary>
    /// Active distance constraints.
    /// </summary>
    public IReadOnlyList<DistanceConstraint> Constraints => _constraints;

    private readonly List<DistanceConstraint> _constraints = new();
    private readonly List<(int BoneIndex, Vector3 Target)> _positionTargets = new();

    public PinguPhysicsSolver(
        int boneCount,
        float gravity = DefaultGravity,
        float damping = DefaultDamping,
        ILogger<PinguPhysicsSolver>? logger = null,
        Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        Gravity = gravity;
        Damping = damping;
        BoneVelocities = new Vector3[boneCount];
    }

    /// <summary>
    /// Add a distance constraint between two bones.
    /// </summary>
    public void AddDistanceConstraint(int boneA, int boneB, float? restLength = null, float? stiffness = null)
    {
        _constraints.Add(new DistanceConstraint(boneA, boneB, restLength ?? DefaultRestLength, stiffness ?? 1.0f));
    }

    /// <summary>
    /// Remove a distance constraint.
    /// </summary>
    public void RemoveDistanceConstraint(int boneA, int boneB)
    {
        _constraints.RemoveAll(c => c.BoneA == boneA && c.BoneB == boneB);
    }

    /// <summary>
    /// Set a target position for a specific bone.
    /// </summary>
    public void SetPositionTarget(int boneIndex, Vector3 target, float stiffness = DefaultSpringStiffness)
    {
        var existing = _positionTargets.Find(t => t.BoneIndex == boneIndex);
        if (existing.BoneIndex >= 0)
        {
            _positionTargets.Remove(existing);
        }
        _positionTargets.Add((boneIndex, target));
    }

    /// <summary>
    /// Clear all position targets.
    /// </summary>
    public void ClearPositionTargets()
    {
        _positionTargets.Clear();
    }

    /// <summary>
    /// Solve physics for all bones.
    /// </summary>
    public void Solve(List<Vector3> bonePositions, float deltaTime)
    {
        if (deltaTime <= 0) return;

        // Apply gravity to all bones
        for (var i = 0; i < bonePositions.Count; i++)
        {
            var pos = bonePositions[i];
            pos.Y += Gravity * deltaTime;
            bonePositions[i] = pos;
        }

        // Apply position targets as spring forces
        foreach (var (boneIndex, target) in _positionTargets)
        {
            var displacement = target - bonePositions[boneIndex];
            BoneVelocities[boneIndex] += displacement * DefaultSpringStiffness * deltaTime;
        }

        // Apply velocity damping
        for (var i = 0; i < bonePositions.Count; i++)
        {
            BoneVelocities[i] *= Damping;
        }

        // Solve distance constraints (multiple iterations for stability)
        for (var iter = 0; iter < 5; iter++)
        {
            foreach (var constraint in _constraints)
            {
                SolveDistanceConstraint(bonePositions, constraint, deltaTime);
            }
        }
    }

    /// <summary>
    /// Solve a single distance constraint.
    /// </summary>
    private void SolveDistanceConstraint(List<Vector3> positions, DistanceConstraint constraint, float deltaTime)
    {
        var posA = positions[constraint.BoneA];
        var posB = positions[constraint.BoneB];
        var delta = posB - posA;
        var distance = delta.Length();

        if (distance < 0.0001f) distance = 0.0001f;

        var error = (distance - constraint.RestLength) / distance;
        var correction = delta * error * constraint.Stiffness;

        var newA = posA + correction * 0.5f;
        var newB = posB - correction * 0.5f;
        positions[constraint.BoneA] = newA;
        positions[constraint.BoneB] = newB;
    }

    /// <summary>
    /// Apply a force to a specific bone.
    /// </summary>
    public void ApplyForce(int boneIndex, Vector3 force)
    {
        BoneVelocities[boneIndex] += force;
    }

    /// <summary>
    /// Get the current velocity of a bone.
    /// </summary>
    public Vector3 GetVelocity(int boneIndex) => BoneVelocities[boneIndex];

    /// <summary>
    /// Set the velocity of a bone.
    /// </summary>
    public void SetVelocity(int boneIndex, Vector3 velocity)
    {
        BoneVelocities[boneIndex] = velocity;
    }

    /// <summary>
    /// Reset all velocities to zero.
    /// </summary>
    public void ResetVelocities()
    {
        for (var i = 0; i < BoneVelocities.Length; i++)
        {
            BoneVelocities[i] = Vector3.Zero;
        }
    }
}