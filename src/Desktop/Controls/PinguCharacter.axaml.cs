using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
    private readonly ILogger<PinguCharacter> _logger = null!;
    private readonly PinguCharacterView _characterView;
    private readonly PinguAnimationStateMachine _stateMachine;

    public PinguCharacter(
        PinguAnimationSystem animationSystem,
        PinguAnimationStateMachine stateMachine,
        PinguBehaviorTriggers behaviorTriggers,
        PinguPhysicsSolver physicsSolver,
        PinguInverseKinematics ik,
        PinguToolHolder toolHolder,
        PinguRenderer pinguRenderer,
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
            pinguRenderer,
            logger: null);

        // Replace the CharacterView reference from XAML with our injected one
        // Since PinguCharacterView is now a code-only control, find it by type
        var existing = this.FindDescendantOfType<PinguCharacterView>();
        if (existing != null)
        {
            var parent = existing.Parent;
            if (parent is StackPanel panel)
            {
                panel.Children.Remove(existing);
                panel.Children.Add(_characterView);
            }
            else
            {
                // Fallback: replace the entire content
                var border = this.Find<Avalonia.Controls.Border>("pinguCharacterBorder");
                if (border != null)
                {
                    border.Child = _characterView;
                }
            }
        }
        else
        {
            // Fallback: if CharacterView not found by type, set as border content
            var border = this.Find<Avalonia.Controls.Border>("pinguCharacterBorder");
            if (border != null)
            {
                border.Child = _characterView;
            }
        }

        // Start the render loop after the control is attached to the visual tree
        _characterView.StartRenderLoop();

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
        _characterView?.StopRenderLoop();
        _characterView?.Dispose();
    }
}
