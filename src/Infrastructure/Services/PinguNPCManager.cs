using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using DomainModels = OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages Pingu NPC characters, their tasks, roles, tools, and movement.
/// </summary>
public class PinguNPCManager
{
    private readonly ILogger<PinguNPCManager>? _logger;
    private readonly List<PinguNPC> _penguins;
    private readonly Random _random;

    public PinguNPCManager(ILogger<PinguNPCManager>? logger = null, Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        _penguins = new List<PinguNPC>();
    }

    /// <summary>
    /// Get the primary Pingu character.
    /// </summary>
    public PinguNPC Pingu => _penguins.FirstOrDefault(p => p.IsPrimary) ?? CreatePrimaryPingu();

    /// <summary>
    /// Get all penguins.
    /// </summary>
    public IReadOnlyList<PinguNPC> Penguins => _penguins.AsReadOnly();

    /// <summary>
    /// Add a guest penguin.
    /// </summary>
    public PinguNPC AddGuestPingu(string name = "Guest")
    {
        var guest = new PinguNPC
        {
            Name = name,
            IsPrimary = false,
            BodyColor = GetRandomColor(),
            BellyColor = GetRandomColor(),
            BeakColor = GetRandomColor(),
        };
        _penguins.Add(guest);
        return guest;
    }

    /// <summary>
    /// Move a penguin to a target position.
    /// </summary>
    public void MoveTo(PinguNPC pingu, float targetX, float targetY, float duration)
    {
        pingu.TargetX = targetX;
        pingu.TargetY = targetY;
        pingu.MoveDuration = duration;
        pingu.IsMoving = true;
    }

    /// <summary>
    /// Set the role of a penguin.
    /// </summary>
    public void SetRole(PinguNPC pingu, PinguRole role)
    {
        pingu.CurrentRole = role;
        // Trigger hat animation
        pingu.Hat = new PinguHat
        {
            Name = role.ToString() + " Hat",
            HatType = role.ToString(),
            AttachedBoneIndex = 4,
            Color = GetRandomColor(),
        };
    }

    /// <summary>
    /// Equip a tool to a penguin.
    /// </summary>
    public void EquipTool(PinguNPC pingu, PinguToolType tool)
    {
        pingu.CurrentTool = tool;
        pingu.CurrentTask = new DomainModels.PinguTask
        {
            Id = Guid.NewGuid(),
            Description = $"Equipped {tool}",
            Type = DomainModels.PinguTaskType.Workflow,
            Status = DomainModels.PinguTaskStatus.Running,
        };
    }

    /// <summary>
    /// Queue a task for a penguin.
    /// </summary>
    public void QueueTask(PinguNPC pingu, DomainModels.PinguTask task)
    {
        pingu.TaskQueue.Add(task);
    }

    /// <summary>
    /// Update all penguins for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var pingu in _penguins)
        {
            UpdatePingu(pingu, deltaTime);
        }
    }

    /// <summary>
    /// Remove a penguin.
    /// </summary>
    public void RemovePingu(PinguNPC pingu)
    {
        _penguins.Remove(pingu);
    }

    /// <summary>
    /// Create the primary penguin.
    /// </summary>
    private PinguNPC CreatePrimaryPingu()
    {
        var pingu = new PinguNPC
        {
            Name = "Pingu",
            IsPrimary = true,
            BodyColor = "#FFFFFF",
            BellyColor = "#F0F0F0",
            BeakColor = "#FFA500",
            X = 100f,
            Y = 100f,
            CurrentRole = PinguRole.Idle,
            CurrentTool = PinguToolType.None,
        };
        _penguins.Add(pingu);
        return pingu;
    }

    private void UpdatePingu(PinguNPC pingu, float deltaTime)
    {
        // Update movement
        var dx = pingu.TargetX - pingu.X;
        var dy = pingu.TargetY - pingu.Y;
        var dist = (float)Math.Sqrt(dx * dx + dy * dy);

        if (dist > 1f)
        {
            pingu.VelocityX = dx / dist * pingu.Physics.MaxWalkSpeed;
            pingu.VelocityY = dy / dist * pingu.Physics.MaxWalkSpeed;
            pingu.X += pingu.VelocityX * deltaTime;
            pingu.Y += pingu.VelocityY * deltaTime;
            pingu.Rotation = (float)Math.Atan2(dy, dx);
        }
        else
        {
            pingu.VelocityX *= pingu.Physics.VelocityDamping;
            pingu.VelocityY *= pingu.Physics.VelocityDamping;
            pingu.IsMoving = false;
        }

        // Update task queue
        if (pingu.CurrentTask == null && pingu.TaskQueue.Count > 0)
        {
            pingu.CurrentTask = pingu.TaskQueue[0];
            pingu.TaskQueue.RemoveAt(0);
            pingu.CurrentTask.Status = DomainModels.PinguTaskStatus.Running;
        }
    }

    private string GetRandomColor()
    {
        var r = _random.Next(0, 256);
        var g = _random.Next(0, 256);
        var b = _random.Next(0, 256);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}