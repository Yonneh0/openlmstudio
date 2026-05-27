namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Shared base class for items that attach to a Pingu's bone hierarchy (hats, tools, etc.).
/// </summary>
public abstract class PinguAccessory
{
    /// <summary>
    /// Display name of the accessory.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The bone index this accessory attaches to.
    /// </summary>
    public int AttachedBoneIndex { get; set; }

    /// <summary>
    /// Offset from the attached bone (in pixels).
    /// </summary>
    public float OffsetX { get; set; }

    /// <summary>
    /// Offset from the attached bone (in pixels).
    /// </summary>
    public float OffsetY { get; set; }

    /// <summary>
    /// Offset from the attached bone (in pixels).
    /// </summary>
    public float OffsetZ { get; set; }

    /// <summary>
    /// Rotation offset in degrees.
    /// </summary>
    public float Roll { get; set; }

    /// <summary>
    /// Rotation offset in degrees.
    /// </summary>
    public float Pitch { get; set; }

    /// <summary>
    /// Rotation offset in degrees.
    /// </summary>
    public float Yaw { get; set; }

    /// <summary>
    /// Scale multiplier.
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Color of the accessory (hex string).
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// Creates a shallow copy of this accessory (copies scalar properties but not Parent/Children references).
    /// </summary>
    public PinguAccessory Clone() => (PinguAccessory)MemberwiseClone();
}
