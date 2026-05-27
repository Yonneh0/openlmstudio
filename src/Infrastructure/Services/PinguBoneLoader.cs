using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Loads and saves Pingu bone hierarchy and animation data from/to JSON.
/// </summary>
public class PinguBoneLoader
{
    private readonly ILogger<PinguBoneLoader>? _logger;

    public PinguBoneLoader(ILogger<PinguBoneLoader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load bone hierarchy from JSON.
    /// </summary>
    public PinguBoneHierarchy LoadBoneHierarchy(string json)
    {
        var definitions = JsonSerializer.Deserialize<List<PinguBoneDefinition>>(json);
        if (definitions == null)
            throw new ArgumentException("Invalid bone hierarchy JSON");

        var hierarchy = new PinguBoneHierarchy { Definitions = definitions };
        hierarchy.Resolve();
        return hierarchy;
    }

    /// <summary>
    /// Save bone hierarchy to JSON.
    /// </summary>
    public string SaveBoneHierarchy(PinguBoneHierarchy hierarchy)
    {
        return JsonSerializer.Serialize(hierarchy.Definitions, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Load animation clips from JSON.
    /// </summary>
    public List<PinguAnimationClip> LoadAnimationClips(string json)
    {
        return JsonSerializer.Deserialize<List<PinguAnimationClip>>(json) ?? new List<PinguAnimationClip>();
    }

    /// <summary>
    /// Save animation clips to JSON.
    /// </summary>
    public string SaveAnimationClips(List<PinguAnimationClip> clips)
    {
        return JsonSerializer.Serialize(clips, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Load physics parameters from JSON.
    /// </summary>
    public PinguPhysicsParams LoadPhysicsParams(string json)
    {
        return JsonSerializer.Deserialize<PinguPhysicsParams>(json) ?? new PinguPhysicsParams();
    }

    /// <summary>
    /// Save physics parameters to JSON.
    /// </summary>
    public string SavePhysicsParams(PinguPhysicsParams paramsData)
    {
        return JsonSerializer.Serialize(paramsData, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Load a PinguHomeScene with default objects.
    /// </summary>
    public PinguHomeScene LoadHomeScene(string? boneHierarchyJson = null)
    {
        var scene = new PinguHomeScene
        {
            Name = "Default Home",
            BackgroundColor = "#1E1E22",
            Width = 200f,
            Height = 200f,
            Depth = 100f,
            Objects = new List<PinguHomeObject>
            {
                PinguHomeScene.CreateDefaultIgloo(),
                PinguHomeScene.CreateDefaultSink(),
                PinguHomeScene.CreateDefaultRug(),
                PinguHomeScene.CreateDefaultBall(),
                PinguHomeScene.CreateDefaultFishBowl(),
                PinguHomeScene.CreateDefaultNest(),
            }
        };

        // If boneHierarchyJson is provided, parse and apply it
        if (!string.IsNullOrWhiteSpace(boneHierarchyJson))
        {
            try
            {
                var definitions = System.Text.Json.JsonSerializer.Deserialize<List<PinguBoneDefinition>>(boneHierarchyJson);
                if (definitions != null && definitions.Count > 0)
                {
                    scene.BoneHierarchyJson = boneHierarchyJson;

                    // Use the bone hierarchy to customize the scene
                    var rootBone = definitions.FirstOrDefault(d => d.ParentIndex == null);
                    if (rootBone != null)
                    {
                        // Position the scene based on root bone position
                        scene.X = rootBone.X / 200f;
                        scene.Y = rootBone.Y / 200f;
                    }
                }
            }
            catch
            {
                // If JSON is invalid, just use the default scene
            }
        }

        return scene;
    }
}
