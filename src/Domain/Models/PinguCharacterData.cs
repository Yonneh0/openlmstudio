namespace OpenLMStudio.Domain.Models;

/// <summary>
/// Complete character data for a Pingu, combining mesh, bones, animations, and physics.
/// </summary>
public class PinguCharacterData
{
    public PinguMeshData MeshData { get; set; } = new();
    public PinguBoneHierarchy BoneHierarchy { get; set; } = new();
    public List<PinguAnimationClip> AnimationClips { get; set; } = new();
    public PinguPhysicsParams PhysicsParams { get; set; } = new();
    public byte[]? TextureAtlas { get; set; }
    public int? Seed { get; set; }
}