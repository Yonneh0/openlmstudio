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
    public PinguAnimationState CurrentAnimation { get; set; } = PinguAnimationState.Idle;
    public PinguNPCState State { get; set; } = PinguNPCState.Idle;

    // Task queue
    public List<PinguTask> TaskQueue { get; set; } = new();
    public PinguTask? CurrentTask { get; set; }

    // Hat
    public PinguHat? Hat { get; set; }

    // Tool
    public PinguTool? Tool { get; set; }

    /// <summary>
    /// Convenience accessor for the tool type (null-safe).
    /// </summary>
    public PinguToolType CurrentToolType => Tool?.ToolType ?? PinguToolType.None;

    // Physics
    public PinguPhysicsParams Physics { get; set; } = new();

    /// <summary>
    /// Whether this Pingu has reached its target position.
    /// <remarks>Uses a threshold of 0.1f for both axes — adjust if tighter/looser tolerance is needed.</remarks>
    /// </summary>
    public bool HasReachedTarget => IsMoving && Math.Abs(X - TargetX) < 0.5f && Math.Abs(Y - TargetY) < 0.5f;

    /// <summary>
    /// Moves this Pingu to the target position over the given duration.
    /// </summary>
    public void MoveTo(float targetX, float targetY, float duration)
    {
        _moveStartX = X;
        _moveStartY = Y;
        TargetX = targetX;
        TargetY = targetY;
        MoveDuration = duration;
        MoveProgress = 0f;
        IsMoving = true;

        // Calculate velocity for has-reached-target detection
        var dx = targetX - _moveStartX;
        var dy = targetY - _moveStartY;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        VelocityX = distance > 0 ? dx / distance : 0f;
        VelocityY = distance > 0 ? dy / distance : 0f;
    }

    /// <summary>
    /// Updates the Pingu's position based on the current move progress (call each frame).
    /// Uses smooth easing (ease-in-out) for natural movement.
    /// </summary>
    public void UpdatePosition(float deltaTime = 1f / 60f)
    {
        if (!IsMoving) return;

        MoveProgress += deltaTime / MoveDuration;
        if (MoveProgress >= 1.0f)
        {
            X = TargetX;
            Y = TargetY;
            VelocityX = 0f;
            VelocityY = 0f;
            IsMoving = false;
            MoveProgress = 1f;
        }
        else
        {
            // Smooth easing: ease-in-out cubic
            var t = MoveProgress;
            var easedT = t < 0.5f ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2f;

            // Calculate position from start to target using eased progress
            var dx = TargetX - _moveStartX;
            var dy = TargetY - _moveStartY;
            X = _moveStartX + dx * (float)easedT;
            Y = _moveStartY + dy * (float)easedT;

            // Update velocity based on movement
            VelocityX = dx / MoveDuration;
            VelocityY = dy / MoveDuration;
        }
    }

    /// <summary>
    /// Start position for movement interpolation (set when MoveTo is called).
    /// </summary>
    private float _moveStartX;
    private float _moveStartY;
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