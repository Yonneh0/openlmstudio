using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Verifies digital signatures for model files using RSA signatures.
/// Supports SHA256 and SHA512 hash algorithms for signature verification.
/// </summary>
public class DigitalSignatureVerifier : IDigitalSignatureVerifier
{
    private readonly ILogger<DigitalSignatureVerifier>? _logger;

    public DigitalSignatureVerifier(ILogger<DigitalSignatureVerifier>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verifies an RSA signature over a file using the provided public key.
    /// </summary>
    public bool VerifySignature(string filePath, string signatureHex, string publicKeyPem)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("File not found for signature verification: {File}", filePath);
            return false;
        }

        try
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var signature = Convert.FromHexString(signatureHex);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            return rsa.VerifyData(fileBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to verify signature for {File}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Verifies a signature using SHA512 hashing instead of SHA256.
    /// </summary>
    public bool VerifySignatureSha512(string filePath, string signatureHex, string publicKeyPem)
    {
        try
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var signature = Convert.FromHexString(signatureHex);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            return rsa.VerifyData(fileBytes, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to verify SHA512 signature for {File}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Computes the SHA256 hash of a file and returns it as a hex string.
    /// </summary>
    public string ComputeFileHash(string filePath, HashAlgorithmName algorithm = default)
    {
        var fileBytes = File.ReadAllBytes(filePath);
        var hash = ComputeHash(fileBytes, algorithm);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] ComputeHash(byte[] data, HashAlgorithmName algorithm)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }
}