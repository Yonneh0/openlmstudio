// Brought to you by Carls' Jr.
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Plugin security validator that verifies provenance (SHA256 hash verification) and manifest integrity
/// before allowing plugin installation or loading.
/// </summary>
public class PluginSecurityValidator : IPluginSecurityValidator
{
    private readonly ILogger<PluginSecurityValidator>? _logger;

    public PluginSecurityValidator(ILogger<PluginSecurityValidator>? logger)
    {
        _logger = logger;
    }

    public Task<bool> VerifyPluginHashAsync(byte[] data, string expectedHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            _logger?.LogWarning("No expected hash provided for plugin verification");
            return Task.FromResult(false);
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(data));
        var matches = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

        if (matches)
        {
            _logger?.LogInformation("Plugin hash verified successfully");
        }
        else
        {
            _logger?.LogWarning("Plugin hash verification failed — expected: {Expected}, actual: {Actual}", expectedHash, actualHash);
        }

        return Task.FromResult(matches);
    }

    public PluginVerificationResult VerifyManifestIntegrity(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return PluginVerificationResult.Fail("Manifest file not found");

        try
        {
            var content = File.ReadAllText(manifestPath);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            if (manifest == null)
                return PluginVerificationResult.Fail("Manifest could not be parsed as JSON");

            // Required fields
            if (!manifest.ContainsKey("Id") || string.IsNullOrWhiteSpace(manifest["Id"]?.ToString()))
                return PluginVerificationResult.Fail("Missing or empty 'Id' field in manifest");

            if (!manifest.ContainsKey("Name") || string.IsNullOrWhiteSpace(manifest["Name"]?.ToString()))
                return PluginVerificationResult.Fail("Missing or empty 'Name' field in manifest");

            // Optional but recommended: check for version field
            if (!manifest.ContainsKey("Version"))
                _logger?.LogWarning("Manifest missing 'Version' field for plugin: {Id}", manifest["Id"]);

            // Security check: reject manifests with suspicious fields
            var dangerousKeys = new[] { "postInstall", "preLoad", "runAtStartup", "systemCall" };
            foreach (var key in dangerousKeys)
            {
                if (manifest.ContainsKey(key))
                    return PluginVerificationResult.Fail($"Manifest contains dangerous field '{key}'");
            }

            return PluginVerificationResult.Success();
        }
        catch (Exception ex)
        {
            return PluginVerificationResult.Fail($"Manifest verification error: {ex.Message}");
        }
    }

    public async Task<PluginVerificationResult> VerifyPluginArchiveAsync(byte[] archiveData, CancellationToken ct = default)
    {
        // Validate archive is a valid ZIP
        try
        {
            using var archiveStream = new MemoryStream(archiveData);
            using var archive = new System.IO.Compression.ZipArchive(archiveStream, System.IO.Compression.ZipArchiveMode.Read);

            // Check for malicious entries (path traversal, executable in root)
            var hasManifest = false;
            foreach (var entry in archive.Entries)
            {
                // Reject entries that try to escape the archive
                if (entry.FullName.Contains(".."))
                    return PluginVerificationResult.Fail($"Archive entry contains path traversal: {entry.FullName}");

                // Reject .exe files at root level
                if (entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    entry.FullName.IndexOf('/') == -1)
                    return PluginVerificationResult.Fail("Root-level executable found in archive");

                // Check for manifest
                if (entry.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                    hasManifest = true;
            }

            if (!hasManifest)
                return PluginVerificationResult.Fail("Archive does not contain a manifest.json file");

            return PluginVerificationResult.Success();
        }
        catch (Exception ex)
        {
            return PluginVerificationResult.Fail($"Archive validation failed: {ex.Message}");
        }
    }
}

