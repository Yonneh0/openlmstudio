namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a workflow executed by the System AI Coordinator.
/// </summary>
public record Workflow(
    string Type,
    string Description,
    string[] Steps);

/// <summary>
/// Result of a workflow execution.
/// </summary>
public record WorkflowResult(
    bool Success,
    string Message,
    string? Output,
    string? Error);

/// <summary>
/// Orchestrates System AI with QEMU VMs for cross-architecture workflows.
/// </summary>
public interface ISystemAICoordinator
{
    /// <summary>
    /// Parses a command and executes the appropriate workflow.
    /// </summary>
    Task<WorkflowResult> HandleCommandAsync(string command);

    /// <summary>
    /// Gets an existing running VM for the architecture, or creates a new one.
    /// Returns the VM ID.
    /// </summary>
    Task<string?> GetOrCreateArchVMAsync(Domain.Models.QEMU.ArchitectureType arch);
}
