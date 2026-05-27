namespace OpenLMStudio.Domain.Models;

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
/// A hat that can be worn by a Pingu character.
/// <remarks>Extends PinguAccessory for bone attachment and transform offsets. Adds HatType-specific properties.</remarks>
/// </summary>
public class PinguHat : PinguAccessory
{
    /// <summary>
    /// Type identifier for the hat (e.g., Crown, Cap, Beanie).
    /// </summary>
    public PinguHatType HatType { get; set; }

    /// <summary>
    /// Animation played when the hat is put on.
    /// </summary>
    public string PutOnAnimation { get; set; } = "hatPutOn";

    /// <summary>
    /// Animation played when the hat is taken off.
    /// </summary>
    public string TakeOffAnimation { get; set; } = "hatTakeOff";

    /// <summary>
    /// Whether the hat is currently equipped on the Pingu.
    /// </summary>
    public bool IsEquipped { get; set; }

    /// <summary>
    /// Creates a copy of this hat.
    /// </summary>
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
