namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A hat that can be worn by a Pingu character.
/// </summary>
public class PinguHat
{
    public string Name { get; set; } = string.Empty;
    public string HatType { get; set; } = string.Empty;

    /// <summary>
    /// The bone index this hat attaches to (usually the head bone).
    /// </summary>
    public int AttachedBoneIndex { get; set; }

    /// <summary>
    /// Offset from the attached bone (in pixels).
    /// </summary>
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }

    /// <summary>
    /// Rotation offset in degrees.
    /// </summary>
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }

    /// <summary>
    /// Scale multiplier.
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Color of the hat.
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// Animation played when the hat is put on.
    /// </summary>
    public string PutOnAnimation { get; set; } = "hatPutOn";

    /// <summary>
    /// Animation played when the hat is taken off.
    /// </summary>
    public string TakeOffAnimation { get; set; } = "hatTakeOff";
}