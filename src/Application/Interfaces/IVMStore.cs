using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Reactive store for managed VM instances (Zustand equivalent).
/// </summary>
public interface IVMStore
{
    /// <summary>
    /// Current VM instances.
    /// </summary>
    IEnumerable<VMInstance> Instances { get; }

    /// <summary>
    /// Event fired when VM state changes.
    /// </summary>
    event EventHandler? OnStateChanged;

    /// <summary>
    /// Adds a VM instance.
    /// </summary>
    Task AddAsync(VMInstance vm);

    /// <summary>
    /// Removes a VM instance.
    /// </summary>
    Task RemoveAsync(string vmId);

    /// <summary>
    /// Gets a VM instance by ID.
    /// </summary>
    Task<VMInstance?> GetAsync(string vmId);

    /// <summary>
    /// Updates a VM instance.
    /// </summary>
    Task UpdateAsync(VMInstance vm);
}