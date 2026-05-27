namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Per-vertex skin weights for a single bone influence.
/// </summary>
public record PinguSkinInfluence
{
    /// <summary>
    /// Index of the bone this influence affects.
    /// </summary>
    public int BoneIndex { get; init; }

    /// <summary>
    /// Weight of this influence (0 to 1).
    /// </summary>
    public float Weight { get; set; }
}

/// <summary>
/// Complete skinning data for a single vertex (4 bone influences).
/// <remarks>This class is defined but not currently used by PinguVertex, which stores skin data inline.</remarks>
/// </summary>
public class PinguVertexSkinData
{
    /// <summary>
    /// The four bone influences for this vertex.
    /// </summary>
    public PinguSkinInfluence[] Influences { get; set; } = new PinguSkinInfluence[4];

    /// <summary>
    /// Creates a new instance with all influence slots initialized.
    /// </summary>
    public PinguVertexSkinData()
    {
        Initialize();
    }

    /// <summary>
    /// Initialize all influence slots with default bone index 0 and weight 0.
    /// </summary>
    public void Initialize()
    {
        for (int i = 0; i < Influences.Length; i++)
        {
            Influences[i] = new PinguSkinInfluence { BoneIndex = 0, Weight = 0f };
        }
    }

    /// <summary>
    /// Get the bone index at the given influence slot.
    /// </summary>
    /// <param name="slot">Slot index (0-3). Returns 0 if out of range.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when slot is outside the valid range.</exception>
    public int GetBoneIndex(int slot)
    {
        if (slot < 0 || slot >= Influences.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot index must be between 0 and 3.");
        return Influences[slot]?.BoneIndex ?? 0;
    }

    /// <summary>
    /// Get the weight at the given influence slot.
    /// </summary>
    /// <param name="slot">Slot index (0-3). Returns 0 if out of range.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when slot is outside the valid range.</exception>
    public float GetWeight(int slot)
    {
        if (slot < 0 || slot >= Influences.Length)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot index must be between 0 and 3.");
        return Influences[slot]?.Weight ?? 0f;
    }
}
