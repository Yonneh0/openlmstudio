using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Provides architecture-specific system prompts for QEMU VMs.
/// </summary>
public interface IArchPromptService
{
    /// <summary>
    /// Gets the system prompt for a given architecture.
    /// </summary>
    string GetSystemPrompt(ArchitectureType arch);

    /// <summary>
    /// Gets cross-compile environment variable settings for a given architecture.
    /// </summary>
    string GetCrossCompileEnvVars(ArchitectureType arch);
}