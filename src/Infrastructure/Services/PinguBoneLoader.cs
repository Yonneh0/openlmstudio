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
}