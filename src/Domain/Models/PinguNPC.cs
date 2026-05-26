namespace OpenLMStudio.Domain.Models;

/// <summary>
/// The role of a Pingu character in the NPC system.
/// </summary>
public enum PinguRole
{
    Idle,
    Worker,
    Explorer,
    Assistant,
    Guardian,
    Artist,
    Chef,
    Scientist,
    Wandering,
}

/// <summary>
/// The type of tool a Pingu is holding.
/// </summary>
public enum PinguToolType
{
    None,
    Pickaxe,
    Sledgehammer,
    PokeStick,
    Paintbrush,
    ChefHat,
    LabCoat,
    Crown,
    Hat,
}

/// <summary>
/// A pending task in the NPC queue.
/// </summary>
public class PinguTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public PinguTaskType Type { get; set; }
    public int Priority { get; set; } = 0;
    public PinguTaskStatus Status { get; set; } = PinguTaskStatus.Pending;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
}

/// <summary>
/// The current status of a Pingu task.
/// </summary>
public enum PinguTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// A Pingu NPC character with role, tool, and task state.
/// </summary>
public class PinguNPC
{
    public string Name { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    // Appearance
    public string BodyColor { get; set; } = "#FFFFFF";
    public string BellyColor { get; set; } = "#F0F0F0";
    public string BeakColor { get; set; } = "#FFA500";
    public float HeightScale { get; set; } = 1.0f;
    public float WidthScale { get; set; } = 1.0f;

    // Position
    public float X { get; set; }
    public float Y { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float Rotation { get; set; }
    public float TargetRotation { get; set; }

    // Movement
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public bool IsMoving { get; set; }
    public float MoveProgress { get; set; }
    public float MoveDuration { get; set; }

    // State
    public PinguRole CurrentRole { get; set; } = PinguRole.Idle;
    public PinguToolType CurrentTool { get; set; } = PinguToolType.None;
    public PinguAnimationState CurrentAnimation { get; set; } = PinguAnimationState.Idle;
    public PinguNPCState State { get; set; } = PinguNPCState.Idle;

    // Task queue
    public List<PinguTask> TaskQueue { get; set; } = new();
    public PinguTask? CurrentTask { get; set; }

    // Hat
    public PinguHat? Hat { get; set; }

    // Tool (in addition to the CurrentTool enum for quick checks)
    public PinguTool? Tool { get; set; }

    // Physics
    public PinguPhysicsParams Physics { get; set; } = new();

    /// <summary>
    /// Whether this Pingu has reached its target position.
    /// </summary>
    public bool HasReachedTarget => Math.Abs(VelocityX) < 0.1f && Math.Abs(VelocityY) < 0.1f;
}

/// <summary>
/// The current operational state of a Pingu NPC.
/// </summary>
public enum PinguNPCState
{
    Idle,
    Moving,
    Working,
    Playing,
    Eating,
    Sleeping,
    Wandering,
}