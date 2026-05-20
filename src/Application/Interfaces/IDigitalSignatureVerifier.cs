namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Service for verifying digital signatures on model files.
/// Uses RSA signatures with SHA256 or SHA512 hash algorithms.
/// </summary>
public interface IDigitalSignatureVerifier
{
    /// <summary>
    /// Verifies an RSA signature over a file using the provided public key.
    /// </summary>
    bool VerifySignature(string filePath, string signatureHex, string publicKeyPem);

    /// <summary>
    /// Verifies a signature using SHA512 hashing.
    /// </summary>
    bool VerifySignatureSha512(string filePath, string signatureHex, string publicKeyPem);

    /// <summary>
    /// Computes the SHA256 hash of a file.
    /// </summary>
    string ComputeFileHash(string filePath, System.Security.Cryptography.HashAlgorithmName algorithm = default);
}