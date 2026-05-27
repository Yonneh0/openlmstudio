using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Tool holding system for Pingu characters.
/// Manages which bone holds which tool and the tool's position/orientation.
/// </summary>
public class PinguToolHolder
{
    private readonly ILogger<PinguToolHolder>? _logger;
    private readonly Random _random;
    private readonly PinguBoneHierarchy _boneHierarchy;

    /// <summary>
    /// Tool attached to a specific bone.
    /// </summary>
    public record struct ToolAttachment(
        int BoneIndex,
        PinguTool Tool,
        Vector3 OffsetPosition,
        Vector3 OffsetRotation);

    /// <summary>
    /// Current tool attachments.
    /// </summary>
    public IReadOnlyList<ToolAttachment> Attachments => _attachments;

    private readonly List<ToolAttachment> _attachments = new();

    /// <summary>
    /// Tool equip/unequip animation progress.
    /// </summary>
    private readonly Dictionary<PinguToolType, float> _equipProgress;

    /// <summary>
    /// Duration of equip/unequip animations.
    /// </summary>
    private const float EquipDuration = 0.5f;

    public PinguToolHolder(
        PinguBoneHierarchy boneHierarchy,
        ILogger<PinguToolHolder>? logger = null,
        Random? random = null)
    {
        _boneHierarchy = boneHierarchy;
        _logger = logger;
        _random = random ?? new Random();
        _equipProgress = new Dictionary<PinguToolType, float>();
    }

    /// <summary>
    /// Equip a tool on a specific bone.
    /// </summary>
    public void EquipTool(PinguTool tool, int boneIndex = 8)
    {
        // Remove existing tool of same type
        var existing = _attachments.Find(a => a.Tool.ToolType == tool.ToolType);
        if (existing.BoneIndex >= 0)
        {
            _attachments.Remove(existing);
        }

        _attachments.Add(new ToolAttachment(boneIndex, tool, Vector3.Zero, Vector3.Zero));
        _equipProgress[tool.ToolType] = 0;

        _logger?.LogDebug("Equipped {ToolType} on bone {BoneIndex}", tool.ToolType, boneIndex);
    }

    /// <summary>
    /// Unequip a tool of a specific type.
    /// </summary>
    public void UnequipTool(PinguToolType toolType)
    {
        var existing = _attachments.Find(a => a.Tool.ToolType == toolType);
        if (existing.BoneIndex >= 0)
        {
            _attachments.Remove(existing);
            _equipProgress.Remove(toolType);
            _logger?.LogDebug("Unequipped {ToolType}", toolType);
        }
    }

    /// <summary>
    /// Get the tool currently held by a Pingu.
    /// </summary>
    public PinguTool? GetCurrentTool()
    {
        return _attachments.FirstOrDefault(a => a.Tool.ToolType != PinguToolType.None).Tool;
    }

    /// <summary>
    /// Update tool equip/unequip animations.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var (toolType, progress) in _equipProgress.ToList())
        {
            _equipProgress[toolType] = Math.Min(1, progress + deltaTime / EquipDuration);
            if (_equipProgress[toolType] >= 1)
            {
                _equipProgress.Remove(toolType);
            }
        }
    }

    /// <summary>
    /// Get the world position of a tool attachment.
    /// </summary>
    public Vector3 GetToolPosition(int attachmentIndex, IReadOnlyList<Vector3> bonePositions)
    {
        if (attachmentIndex >= _attachments.Count)
            return Vector3.Zero;

        var attachment = _attachments[attachmentIndex];
        var bonePos = bonePositions[attachment.BoneIndex];
        return bonePos + attachment.OffsetPosition;
    }

    /// <summary>
    /// Get the current equip progress for a tool type.
    /// </summary>
    public float GetEquipProgress(PinguToolType toolType)
    {
        return _equipProgress.GetValueOrDefault(toolType, 1);
    }

    /// <summary>
    /// Clear all tool attachments.
    /// </summary>
    public void ClearAllTools()
    {
        _attachments.Clear();
        _equipProgress.Clear();
    }
}