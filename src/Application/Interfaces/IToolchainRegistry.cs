using OpenLMStudio.Domain.Models.QEMU;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Registry for architecture-specific compiler toolchains.
/// </summary>
public interface IToolchainRegistry
{
    /// <summary>
    /// Gets the path to a toolchain for the given architecture, downloading if needed.
    /// </summary>
    Task<string?> GetToolchainAsync(ArchitectureType arch, string toolName);
}