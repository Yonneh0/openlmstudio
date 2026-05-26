namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A single vertex in the Pingu mesh with position, normal, UV, and skinning data.
/// </summary>
public class PinguVertex
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public float NormalX { get; set; }
    public float NormalY { get; set; }
    public float NormalZ { get; set; }

    public float U { get; set; }
    public float V { get; set; }

    public byte BoneIndex0 { get; set; }
    public byte BoneIndex1 { get; set; }
    public byte BoneIndex2 { get; set; }
    public byte BoneIndex3 { get; set; }

    public float BoneWeight0 { get; set; }
    public float BoneWeight1 { get; set; }
    public float BoneWeight2 { get; set; }
    public float BoneWeight3 { get; set; }
}

/// <summary>
/// A single triangle in the Pingu mesh, referencing three vertex indices.
/// </summary>
public class PinguTriangle
{
    public int Vertex0 { get; set; }
    public int Vertex1 { get; set; }
    public int Vertex2 { get; set; }

    /// <summary>
    /// The Z-order for draw sorting (higher = drawn later/on top).
    /// </summary>
    public float ZOrder { get; set; }
}

/// <summary>
/// Complete mesh data for a Pingu character, including vertices, triangles, and texture atlas reference.
/// </summary>
public class PinguMeshData
{
    public List<PinguVertex> Vertices { get; set; } = new();
    public List<PinguTriangle> Triangles { get; set; } = new();

    /// <summary>
    /// The texture atlas as a byte array (PNG format).
    /// </summary>
    public byte[]? TextureAtlasBytes { get; set; }

    /// <summary>
    /// The width of the texture atlas (default 512).
    /// </summary>
    public int AtlasWidth { get; set; } = 512;

    /// <summary>
    /// The height of the texture atlas (default 512).
    /// </summary>
    public int AtlasHeight { get; set; } = 512;

    /// <summary>
    /// The total vertex count.
    /// </summary>
    public int VertexCount => Vertices.Count;

    /// <summary>
    /// The total triangle count.
    /// </summary>
    public int TriangleCount => Triangles.Count;

    /// <summary>
    /// Get the approximate vertex count for display.
    /// </summary>
    public string VertexCountDisplay => VertexCount >= 1000
        ? $"{VertexCount / 1000.0:F1}K"
        : VertexCount.ToString();

    /// <summary>
    /// Get the approximate triangle count for display.
    /// </summary>
    public string TriangleCountDisplay => TriangleCount >= 1000
        ? $"{TriangleCount / 1000.0:F1}K"
        : TriangleCount.ToString();
}