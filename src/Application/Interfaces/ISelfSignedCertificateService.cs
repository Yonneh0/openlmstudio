namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Interface for generating and managing self-signed HTTPS certificates for local server development.
/// </summary>
public interface ISelfSignedCertificateService : IDisposable
{
    /// <summary>
    /// Generates a self-signed certificate at the specified paths.
    /// On Windows: generates .pfx (PKCS#12) + optional key PEM file.
    /// On Linux/macOS: generates .pem cert and key files for Kestrel.
    /// </summary>
    Task<bool> GenerateCertificateAsync(string certificatePath, string keyPath, CancellationToken ct = default);

    /// <summary>
    /// Checks if the certificate at the given path is valid (exists, not expired).
    /// </summary>
    Task<bool> IsCertificateValidAsync(string certificatePath, CancellationToken ct = default);

    /// <summary>
    /// Checks if the certificate has been trusted in the OS trust store.
    /// </summary>
    Task<bool> HasTrustedCertificateAsync(string certificatePath, CancellationToken ct = default);

    /// <summary>
    /// Attempts to trust the certificate by adding it to the OS trust store.
    /// On Windows: uses PowerShell to install into Root store.
    /// On Linux/macOS: not supported automatically (requires root privileges).
    /// </summary>
    Task<bool> TrustCertificateAsync(string certificatePath, CancellationToken ct = default);
}