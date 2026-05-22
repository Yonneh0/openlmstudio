// Brought to you by Carls' Jr.
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Contract for plugin security validation — hash verification, manifest integrity, and archive validation.
/// </summary>
public interface IPluginSecurityValidator
{
    /// <summary>
    /// Verifies that the plugin archive data matches the expected SHA256 hash.
    /// </summary>
    Task<bool> VerifyPluginHashAsync(byte[] data, string expectedHash, CancellationToken ct = default);

    /// <summary>
    /// Validates the integrity of a plugin manifest file (checks required fields, rejects dangerous keys).
    /// </summary>
    PluginVerificationResult VerifyManifestIntegrity(string manifestPath);

    /// <summary>
    /// Validates a plugin archive (ZIP) — checks for path traversal, root-level executables, and manifest presence.
    /// </summary>
    Task<PluginVerificationResult> VerifyPluginArchiveAsync(byte[] archiveData, CancellationToken ct = default);
}