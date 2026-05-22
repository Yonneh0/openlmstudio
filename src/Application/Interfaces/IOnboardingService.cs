namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service that tracks onboarding progress and surfaces the onboarding dialog.
/// </summary>
public interface IOnboardingService
{
    /// <summary>Whether the user has completed onboarding.</summary>
    bool IsComplete { get; }

    /// <summary>
    /// Shows the onboarding dialog (first run only).
    /// Returns true if onboarding was shown and accepted.
    /// </summary>
    Task<bool> ShowOnboardingAsync();

    /// <summary>
    /// Marks onboarding as complete.
    /// </summary>
    Task MarkCompleteAsync();
}