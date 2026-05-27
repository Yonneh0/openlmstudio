namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A tool that a Pingu can hold and use.
/// <remarks>Extends PinguAccessory for bone attachment and transform offsets. Adds ToolType-specific properties.</remarks>
/// </summary>
public class PinguTool : PinguAccessory
{
    /// <summary>
    /// Type of tool (e.g., Pickaxe, Sledgehammer).
    /// </summary>
    public PinguToolType ToolType { get; set; }

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

    /// <summary>
    /// Creates a copy of this tool.
    /// </summary>
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
