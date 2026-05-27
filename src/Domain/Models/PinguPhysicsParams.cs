namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Physics parameters for a Pingu character.
/// </summary>
public class PinguPhysicsParams
{
    /// <summary>
    /// Mass of the character in kg.
    /// </summary>
    public float Mass { get; set; } = 1.0f;

    /// <summary>
    /// Friction coefficient for movement (0 = slippery, 1 = sticky).
    /// </summary>
    public float Friction { get; set; } = 0.6f;

    /// <summary>
    /// Gravity applied to the character (pixels per second squared).
    /// </summary>
    public float Gravity { get; set; } = 980.0f;

    /// <summary>
    /// IK stiffness for inverse kinematics (0 = loose, 1 = rigid).
    /// </summary>
    public float IkStiffness { get; set; } = 0.7f;

    /// <summary>
    /// Velocity damping factor (0 = no damping, 1 = full damping).
    /// </summary>
    public float VelocityDamping { get; set; } = 0.95f;

    /// <summary>
    /// Spring stiffness for limb oscillation.
    /// </summary>
    public float SpringStiffness { get; set; } = 0.1f;

    /// <summary>
    /// Rest length for spring joints.
    /// </summary>
    public float SpringRestLength { get; set; } = 1.0f;

    /// <summary>
    /// Whether the character is affected by gravity.
    /// </summary>
    public bool AffectedByGravity { get; set; } = true;

    /// <summary>
    /// Maximum walking speed (pixels per second).
    /// </summary>
    public float MaxWalkSpeed { get; set; } = 100.0f;

    /// <summary>
    /// Maximum running speed (pixels per second).
    /// </summary>
    public float MaxRunSpeed { get; set; } = 200.0f;

    /// <summary>
    /// Acceleration rate when starting movement.
    /// </summary>
    public float Acceleration { get; set; } = 300.0f;

    /// <summary>
    /// Deceleration rate when stopping movement.
    /// </summary>
    public float Deceleration { get; set; } = 400.0f;

    /// <summary>
    /// Validates that all physics parameters are within reasonable bounds.
    /// </summary>
    public bool IsValid()
    {
        if (Mass <= 0f) return false;
        if (Friction < 0f || Friction > 1f) return false;
        if (Gravity < 0f) return false;
        if (IkStiffness < 0f || IkStiffness > 1f) return false;
        if (VelocityDamping < 0f || VelocityDamping > 1f) return false;
        if (SpringStiffness < 0f) return false;
        if (SpringRestLength < 0f) return false;
        if (MaxWalkSpeed <= 0f) return false;
        if (MaxRunSpeed <= 0f) return false;
        if (Acceleration < 0f) return false;
        if (Deceleration < 0f) return false;
        return true;
    }

    /// <summary>
    /// Creates a copy of this physics params object.
    /// </summary>
    public PinguPhysicsParams Clone() => new()
    {
        Mass = Mass,
        Friction = Friction,
        Gravity = Gravity,
        IkStiffness = IkStiffness,
        VelocityDamping = VelocityDamping,
        SpringStiffness = SpringStiffness,
        SpringRestLength = SpringRestLength,
        AffectedByGravity = AffectedByGravity,
        MaxWalkSpeed = MaxWalkSpeed,
        MaxRunSpeed = MaxRunSpeed,
        Acceleration = Acceleration,
        Deceleration = Deceleration,
    };

    /// <summary>
    /// Validates that MaxWalkSpeed does not exceed MaxRunSpeed.
    /// </summary>
    public bool HasConsistentSpeedLimits() => MaxWalkSpeed <= MaxRunSpeed;
}
