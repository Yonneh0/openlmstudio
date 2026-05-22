using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a step in the onboarding flow.
/// </summary>
public record OnboardingStep(
    string Id,
    string Title,
    string Description,
    string? Icon = null,
    string? ImageUrl = null,
    string? ActionText = null,
    string? ActionUrl = null,
    bool HasSkipButton = true);

/// <summary>
/// Manages the first-run onboarding experience.
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Gets whether onboarding has been completed.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Gets the current step index (0-based).
    /// </summary>
    int CurrentStepIndex { get; }

    /// <summary>
    /// Gets the current onboarding step.
    /// </summary>
    OnboardingStep? CurrentStep { get; }

    /// <summary>
    /// Gets all onboarding steps.
    /// </summary>
    IReadOnlyList<OnboardingStep> GetAllSteps();

    /// <summary>
    /// Navigates to the next step.
    /// </summary>
    void Next();

    /// <summary>
    /// Navigates to the previous step.
    /// </summary>
    void Previous();

    /// <summary>
    /// Skips the onboarding flow.
    /// </summary>
    void Skip();

    /// <summary>
    /// Completes the onboarding flow.
    /// </summary>
    Task CompleteAsync(CancellationToken ct = default);

    /// <summary>
    /// Shows the onboarding flow (can be triggered manually).
    /// </summary>
    void Show();

    /// <summary>
    /// Gets the default onboarding steps for new users.
    /// </summary>
    static IReadOnlyList<OnboardingStep> DefaultSteps => new List<OnboardingStep>
    {
        new OnboardingStep(
            "welcome",
            "Welcome to OpenLMStudio",
            "Your local AI workspace for running and managing large language models on your machine — no cloud required.",
            "🚀",
            null,
            "Get Started"),
        new OnboardingStep(
            "models",
            "Download Models",
            "Browse and download GGUF models for text generation, safetensors models for image generation, diffusion, VAE, and LoRA adapters. Models are stored locally in your appdata directory.",
            "📦",
            null,
            "Open Models Tab"),
        new OnboardingStep(
            "server",
            "Start the Local Server",
            "Start the local inference server to enable chat completions via OpenAI-compatible or Anthropic-compatible endpoints. Choose your model, set context length, and click Start Server.",
            "🖥️",
            null,
            "Open Server Tab"),
        new OnboardingStep(
            "chat",
            "Start Chatting",
            "Create a new chat, select your model, and start generating responses. Use streaming mode for token-by-token responses, or configure context compression for long conversations.",
            "💬",
            null,
            "Open Chat Tab"),
        new OnboardingStep(
            "agent",
            "Try the Agent",
            "The Agent can autonomously complete tasks using tools like file reading, writing, command execution, and git operations. Set iteration limits and watch it work through your tasks.",
            "🤖",
            null,
            "Open Agent Tab"),
        new OnboardingStep(
            "images",
            "Generate Images",
            "Use diffusion models to generate images, inpaint regions, or outpaint canvases. Configure resolution, steps, CFG scale, and sampler type.",
            "🎨",
            null,
            "Open Image Generation Tab"),
        new OnboardingStep(
            "context",
            "Context Management",
            "Fine-tune what the AI sees with context pinning, suppression, and custom injection. The context window budgeter automatically manages token limits.",
            "🧠",
            null,
            "Open Context Panel"),
        new OnboardingStep(
            "done",
            "You're All Set!",
            "OpenLMStudio is ready to use. Explore the settings panel to configure server options, API keys, plugins, and more.",
            "✅",
            null,
            "Done"),
    };
}