namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Per-vertex skin weights for a single bone influence.
/// </summary>
public class PinguSkinInfluence
{
    public int BoneIndex { get; set; }
    public float Weight { get; set; }
}

/// <summary>
/// Complete skinning data for a single vertex (4 bone influences).
/// </summary>
public class PinguVertexSkinData
{
    public PinguSkinInfluence[] Influences { get; set; } = new PinguSkinInfluence[4];

    /// <summary>
    /// Initialize all influence slots with default bone index 0 and weight 0.
    /// </summary>
    public void Initialize()
    {
        for (int i = 0; i < Influences.Length; i++)
        {
            Influences[i] = new PinguSkinInfluence();
        }
    }

    /// <summary>
    /// Get the bone index at the given influence slot.
    /// </summary>
    public int GetBoneIndex(int slot) => Influences[slot]?.BoneIndex ?? 0;

    /// <summary>
    /// Get the weight at the given influence slot.
    /// </summary>
    public float GetWeight(int slot) => Influences[slot]?.Weight ?? 0f;
}

/// <summary>
/// A single skin weight entry in the binary mesh file.
/// </summary>
public class PinguSkinWeightEntry
{
    public byte BoneIndex0 { get; set; }
    public byte BoneIndex1 { get; set; }
    public byte BoneIndex2 { get; set; }
    public byte BoneIndex3 { get; set; }

    public float BoneWeight0 { get; set; }
    public float BoneWeight1 { get; set; }
    public float BoneWeight2 { get; set; }
    public float BoneWeight3 { get; set; }
}