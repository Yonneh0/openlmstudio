namespace OpenLMStudio.Domain.Models;

/// <summary>
/// A static object in the Pingu home scene.
/// </summary>
public class PinguHomeObject
{
    /// <summary>
    /// Display name of the object.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type identifier for the object (e.g., "igloo", "sink", "rug").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Position in the home scene (0-1 normalized).
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Position in the home scene (0-1 normalized).
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Z position in the home scene (0-1 normalized) for depth sorting.
    /// </summary>
    public float Z { get; set; }

    /// <summary>
    /// Size in pixels.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Size in pixels.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Visual color of the object.
    /// </summary>
    public string Color { get; set; } = "#808080";

    /// <summary>
    /// Whether the object can be interacted with.
    /// </summary>
    public bool IsInteractive { get; set; } = false;

    /// <summary>
    /// Animation played when interacting with this object.
    /// </summary>
    public string? InteractionAnimation { get; set; }

    /// <summary>
    /// Whether the object is currently visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Creates a copy of this home object.
    /// </summary>
    public PinguHomeObject Clone() => new()
    {
        Name = Name,
        Type = Type,
        X = X,
        Y = Y,
        Z = Z,
        Width = Width,
        Height = Height,
        Color = Color,
        IsInteractive = IsInteractive,
        InteractionAnimation = InteractionAnimation,
        IsVisible = IsVisible,
    };
}

/// <summary>
/// The Pingu home scene with decorative objects.
/// </summary>
public class PinguHomeScene
{
    public string Name { get; set; } = "Default Home";

    // Background color
    public string BackgroundColor { get; set; } = "#1E1E22";

    // Scene dimensions
    public float Width { get; set; } = 200f;
    public float Height { get; set; } = 200f;

    /// <summary>
    /// Scene depth for 3D positioning.
    /// </summary>
    public float Depth { get; set; } = 100f;

    /// <summary>
    /// Scene position X (for bottom-right corner placement).
    /// </summary>
    public float X { get; set; } = 0f;

    /// <summary>
    /// Scene position Y (for bottom-right corner placement).
    /// </summary>
    public float Y { get; set; } = 0f;

    /// <summary>
    /// JSON representation of the bone hierarchy (if loaded from schema).
    /// </summary>
    public string? BoneHierarchyJson { get; set; }

    // Decorative objects
    public List<PinguHomeObject> Objects { get; set; } = new();

    /// <summary>
    /// Objects sorted by Z-order for proper depth rendering.
    /// </summary>
    public IReadOnlyList<PinguHomeObject> SortedObjects
    {
        get
        {
            if (_sortedObjects == null)
            {
                _sortedObjects = Objects.OrderBy(o => o.Z).ToList();
            }
            return _sortedObjects;
        }
    }

    private List<PinguHomeObject>? _sortedObjects;

    /// <summary>
    /// Call this when objects are added, removed, or moved to invalidate the sorted cache.
    /// </summary>
    public void InvalidateSortedObjects()
    {
        _sortedObjects = null;
    }

    /// <summary>
    /// Get the default igloo object.
    /// </summary>
    public static PinguHomeObject CreateDefaultIgloo() => new()
    {
        Name = "Igloo",
        Type = "igloo",
        X = 0.15f,
        Y = 0.2f,
        Z = 0.2f,
        Width = 80,
        Height = 90,
        Color = "#E8F4F8",
        IsInteractive = true,
        InteractionAnimation = "sit",
    };

    /// <summary>
    /// Get the default sink object.
    /// </summary>
    public static PinguHomeObject CreateDefaultSink() => new()
    {
        Name = "Sink",
        Type = "sink",
        X = 0.6f,
        Y = 0.65f,
        Z = 0.3f,
        Width = 60,
        Height = 40,
        Color = "#B0C4DE",
        IsInteractive = true,
        InteractionAnimation = "wash",
    };

    /// <summary>
    /// Get the default rug object.
    /// </summary>
    public static PinguHomeObject CreateDefaultRug() => new()
    {
        Name = "Rug",
        Type = "rug",
        X = 0.3f,
        Y = 0.75f,
        Z = 0.05f,
        Width = 120,
        Height = 30,
        Color = "#8B4513",
        IsInteractive = false,
    };

    /// <summary>
    /// Get the default ball object.
    /// </summary>
    public static PinguHomeObject CreateDefaultBall() => new()
    {
        Name = "Ball",
        Type = "ball",
        X = 0.75f,
        Y = 0.55f,
        Z = 0.15f,
        Width = 30,
        Height = 30,
        Color = "#FF4500",
        IsInteractive = true,
        InteractionAnimation = "play",
    };

    /// <summary>
    /// Get the default fish bowl object.
    /// </summary>
    public static PinguHomeObject CreateDefaultFishBowl() => new()
    {
        Name = "Fish Bowl",
        Type = "fishbowl",
        X = 0.45f,
        Y = 0.15f,
        Z = 0.4f,
        Width = 50,
        Height = 50,
        Color = "#87CEEB",
        IsInteractive = true,
        InteractionAnimation = "watch",
    };

    /// <summary>
    /// Get the default penguin nest object.
    /// </summary>
    public static PinguHomeObject CreateDefaultNest() => new()
    {
        Name = "Nest",
        Type = "nest",
        X = 0.05f,
        Y = 0.6f,
        Z = 0.1f,
        Width = 70,
        Height = 50,
        Color = "#D2691E",
        IsInteractive = true,
        InteractionAnimation = "sleep",
    };

    /// <summary>
    /// Create a new home scene with all default objects.
    /// </summary>
    public static PinguHomeScene CreateDefault() => new()
    {
        Name = "Default Home",
        BackgroundColor = "#1E1E22",
        Width = 200f,
        Height = 200f,
        Depth = 100f,
        Objects = new List<PinguHomeObject>
        {
            CreateDefaultIgloo(),
            CreateDefaultSink(),
            CreateDefaultRug(),
            CreateDefaultBall(),
            CreateDefaultFishBowl(),
            CreateDefaultNest(),
        },
    };
}
