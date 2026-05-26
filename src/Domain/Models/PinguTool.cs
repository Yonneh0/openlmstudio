namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A tool that a Pingu can hold and use.
/// </summary>
public class PinguTool
{
    public string Name { get; set; } = string.Empty;
    public PinguToolType ToolType { get; set; }

    /// <summary>
    /// The bone index this tool attaches to (usually the right flipper).
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
    /// Color of the tool.
    /// </summary>
    public string Color { get; set; } = "#808080";

    /// <summary>
    /// Animation played when using the tool.
    /// </summary>
    public string UseAnimation { get; set; } = string.Empty;

    /// <summary>
    /// Animation played when equipping the tool.
    /// </summary>
    public string EquipAnimation { get; set; } = "equip";

    /// <summary>
    /// Animation played when unequipping the tool.
    /// </summary>
    public string UnequipAnimation { get; set; } = "unequip";
}