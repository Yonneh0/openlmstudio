using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Rendering;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pingu character control that wraps PinguCharacterView with full service injection.
/// </summary>
public partial class PinguCharacter : UserControl
{
    private readonly ILogger<PinguCharacter>? _logger;
    private readonly PinguCharacterView _characterView;
    private readonly PinguAnimationStateMachine _stateMachine;

    public PinguCharacter(
        PinguAnimationSystem animationSystem,
        PinguAnimationStateMachine stateMachine,
        PinguBehaviorTriggers behaviorTriggers,
        PinguPhysicsSolver physicsSolver,
        PinguInverseKinematics ik,
        PinguToolHolder toolHolder,
        PinguHomeSceneRenderer homeSceneRenderer,
        ILogger<PinguCharacter>? logger = null)
    {
        InitializeComponent();

        _stateMachine = stateMachine;
        // Create the character view with all services
        _characterView = new PinguCharacterView(
            animationSystem,
            stateMachine,
            behaviorTriggers,
            physicsSolver,
            ik,
            toolHolder,
            homeSceneRenderer,
            logger: null);

        // Replace the CharacterView reference
        var existing = this.Find<PinguCharacterView>("CharacterView");
        if (existing != null)
        {
            var panel = existing.Parent as StackPanel;
            if (panel != null)
            {
                panel.Children.Remove(existing);
                panel.Children.Add(_characterView);
            }
        }

        _logger?.LogInformation("PinguCharacter initialized");
    }

    /// <summary>
    /// Update the character for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        _characterView.Update(deltaTime);
    }

    /// <summary>
    /// Set the character name.
    /// </summary>
    public void SetName(string name)
    {
        CharacterName.Text = name;
    }

    /// <summary>
    /// Trigger a specific behavior.
    /// </summary>
    public void TriggerBehavior(PinguAnimationState behavior)
    {
        _stateMachine.TransitionTo(behavior);
    }

    /// <summary>
    /// Dispose of resources.
    /// </summary>
    public void Dispose()
    {
        // Dispose any resources
    }
}