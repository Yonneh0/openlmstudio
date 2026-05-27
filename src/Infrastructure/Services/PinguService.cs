using System.Numerics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Rendering;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Orchestrates the entire Pingu system - mesh generation, animation, NPC, and rendering.
/// </summary>
public class PinguService : IDisposable
{
    private readonly ILogger<PinguService>? _logger;
    private readonly PinguMeshGenerator _meshGenerator;
    private readonly PinguBoneLoader _boneLoader;
    private readonly PinguAnimationSystem _animation;
    private readonly PinguNPCManager _npcManager;
    private readonly PinguRenderer _renderer;
    private readonly PinguHomeScene _homeScene;
    private readonly Random _random;
    private bool _disposed;

    public PinguService(ILogger<PinguService>? logger = null, Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        _meshGenerator = new PinguMeshGenerator();
        _boneLoader = new PinguBoneLoader();

        // Generate or load mesh data
        var meshData = _meshGenerator.Generate(_random.Next());

        // Create bone hierarchy
        var hierarchy = meshData.BoneHierarchy;

        // Create animation system
        _animation = new PinguAnimationSystem(
            hierarchy,
            meshData.AnimationClips,
            meshData.PhysicsParams);

        // Create NPC manager
        _npcManager = new PinguNPCManager();

        // Create home scene using the default factory
        _homeScene = PinguHomeScene.CreateDefault();

        // Create renderer
        _renderer = new PinguRenderer(
            meshData.MeshData,
            hierarchy,
            _animation,
            _npcManager,
            _homeScene,
            meshData.TextureAtlas ?? new byte[0],
            _random);
    }

    /// <summary>
    /// Get the primary Pingu.
    /// </summary>
    public PinguNPC Pingu => _npcManager.Pingu;

    /// <summary>
    /// Get all penguins.
    /// </summary>
    public IReadOnlyList<PinguNPC> Penguins => _npcManager.Penguins;

    /// <summary>
    /// Initialize the renderer.
    /// </summary>
    public void Initialize(int width, int height)
    {
        _renderer.Initialize(width, height);
    }

    /// <summary>
    /// Render the scene.
    /// </summary>
    public void Render(System.Numerics.Vector2 cursorPosition)
    {
        if (_renderer.RenderBitmap != null)
        {
            // Render directly into the renderer's internal bitmap (no redundant canvas creation)
            _renderer.Render(cursorPosition, _renderer.RenderBitmap, new SkiaSharp.SKCanvas(_renderer.RenderBitmap));
        }
    }

    /// <summary>
    /// Update the animation state.
    /// </summary>
    public void Update(float deltaTime)
    {
        _animation.Update(deltaTime);
        _npcManager.Update(deltaTime);
    }

    /// <summary>
    /// Move Pingu to a target position.
    /// </summary>
    public void MovePinguTo(float x, float y, float duration = 2f)
    {
        _npcManager.MoveTo(Pingu, x, y, duration);
    }

    /// <summary>
    /// Set Pingu's role.
    /// </summary>
    public void SetPinguRole(PinguRole role)
    {
        _npcManager.SetRole(Pingu, role);
    }

    /// <summary>
    /// Equip a tool to Pingu.
    /// </summary>
    public void EquipPinguTool(PinguToolType tool)
    {
        _npcManager.EquipTool(Pingu, tool);
    }

    /// <summary>
    /// Queue a task for Pingu.
    /// </summary>
    public void QueuePinguTask(Domain.Models.PinguTask task)
    {
        _npcManager.QueueTask(Pingu, task);
    }

    /// <summary>
    /// Add a guest penguin.
    /// </summary>
    public PinguNPC AddGuestPingu(string name = "Guest")
    {
        return _npcManager.AddGuestPingu(name);
    }

    /// <summary>
    /// Remove a penguin.
    /// </summary>
    public void RemovePingu(PinguNPC pingu)
    {
        _npcManager.RemovePingu(pingu);
    }

    /// <summary>
    /// Trigger a random twitch.
    /// </summary>
    public void TriggerTwitch()
    {
        _animation.TriggerTwitch();
    }

    /// <summary>
    /// Trigger a head turn.
    /// </summary>
    public void TriggerHeadTurn()
    {
        _animation.TriggerHeadTurn();
    }

    /// <summary>
    /// Trigger a scratch.
    /// </summary>
    public void TriggerScratch()
    {
        _animation.TriggerScratch();
    }

    /// <summary>
    /// Trigger a blink.
    /// </summary>
    public void TriggerBlink()
    {
        _animation.TriggerBlink();
    }

    /// <summary>
    /// Trigger sitting down.
    /// </summary>
    public void TriggerSitDown()
    {
        _animation.TriggerSitDown();
    }

    /// <summary>
    /// Trigger sitting up.
    /// </summary>
    public void TriggerSitUp()
    {
        _animation.TriggerSitUp();
    }

    /// <summary>
    /// Dispose the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _renderer?.Dispose();
    }
}