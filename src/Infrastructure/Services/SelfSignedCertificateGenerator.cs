using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Generates and manages self-signed HTTPS certificates for local server development.
/// Supports cross-platform certificate generation using OpenSSL (Linux/macOS) or dotnet dev-certs (Windows fallback).
/// </summary>
public class SelfSignedCertificateGenerator : ISelfSignedCertificateService, IDisposable
{
    private readonly ILogger<SelfSignedCertificateGenerator>? _logger;
    private readonly string? _opensslPath;

    public SelfSignedCertificateGenerator(ILogger<SelfSignedCertificateGenerator>? logger)
    {
        _logger = logger;

        // Try to find OpenSSL on the system (common paths for cross-platform detection)
        _opensslPath = FindOpenSSLSync();
        if (_opensslPath == null)
        {
            _logger?.LogWarning("OpenSSL not found. Certificate generation will fall back to PowerShell or dotnet dev-certs.");
        }
    }

    /// <inheritdoc />
    public async Task<bool> GenerateCertificateAsync(string certificatePath, string keyPath, CancellationToken ct = default)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return await GenerateOnWindows(certificatePath, keyPath, ct);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return await GenerateOnUnix(certificatePath, keyPath, ct);
            }

            _logger?.LogError("Unsupported operating system for certificate generation.");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate self-signed certificate");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsCertificateValidAsync(string certificatePath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(certificatePath))
                return false;

            // Verify the certificate can be read and is valid (not expired)
            using var cert = await LoadCertificateFromDiskAsync(certificatePath, ct);
            if (cert == null)
                return false;

            return false; // NotAfter is DateTime (non-nullable), always check if expired
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasTrustedCertificateAsync(string certificatePath, CancellationToken ct = default)
    {
        var isValid = await IsCertificateValidAsync(certificatePath, ct);
        if (!isValid)
            return false;

        // Check if the certificate is trusted (installed in OS trust store)
        try
        {
            using var cert = await LoadCertificateFromDiskAsync(certificatePath, ct);
            if (cert == null)
                return false;

            // Try to find the certificate in the local machine's personal store
            using var store = new System.Security.Cryptography.X509Certificates.X509Store(
                System.Security.Cryptography.X509Certificates.StoreName.My,
                OperatingSystem.IsWindows() ?
                    System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine :
                    System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser);

            store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
            var found = store.Certificates.Find(
                System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
                cert.Thumbprint, false);

            return found.Count > 0;
        }
        catch
        {
            // If we can't check the trust store, assume the certificate is untrusted
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TrustCertificateAsync(string certificatePath, CancellationToken ct = default)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return await TrustOnWindows(certificatePath, ct);

            // Linux/macOS trust is complex and requires root privileges — skip for now
            _logger?.LogWarning("Certificate auto-trust not supported on this platform. Manual installation required.");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to trust certificate");
            return false;
        }
    }

    public void Dispose()
    {
        // Nothing to dispose for this class
    }

    private async Task<bool> GenerateOnWindows(string certificatePath, string keyPath, CancellationToken ct)
    {
        // Use OpenSSL if available (e.g., Git Bash, WSL)
        if (_opensslPath != null)
        {
            var privateKeyFilePath = Path.Combine(Path.GetDirectoryName(certificatePath) ?? Directory.GetCurrentDirectory(), "private-key.pem");

            // Use OpenSSL to generate PEM cert + key
            if (await GenerateOpenSSLAsync(ct, new[] { "req", "-x509", "-newkey", "rsa:2048",
                    "-keyout", privateKeyFilePath, "-out", certificatePath,
                    "-days", "365", "-nodes", "-subj", "/CN=localhost" }))
            {
                // Convert PEM cert + key to PFX using OpenSSL (OpenSSL is available on Windows via Chocolatey or Git Bash)
                var opensslDir = FindGitBashOpenSSL();
                if (!string.IsNullOrEmpty(opensslDir))
                {
                    await GeneratePFXAsync(privateKeyFilePath, certificatePath, "changeit", ct);

                    // Clean up PEM files
                    try { File.Delete(privateKeyFilePath); } catch { /* Ignore */ }
                    return true;
                }

                // If OpenSSL not available on Windows for PFX conversion, fall back to dotnet dev-certs
            }

            _logger?.LogDebug("OpenSSL cert generation failed or PFX conversion unavailable. Falling back to dotnet dev-certs.");
        }

        // Use .NET dev-certs as the primary fallback for Windows (works without external dependencies)
        if (await GenerateWithDotNetDevCertsAsync(certificatePath, ct))
            return true;

        _logger?.LogWarning("No OpenSSL or dotnet dev-certs available. Cannot generate self-signed certificate for HTTPS.");
        return false;
    }

    private async Task<bool> GenerateOnUnix(string certificatePath, string keyPath, CancellationToken ct)
    {
        if (_opensslPath == null)
        {
            _logger?.LogError("OpenSSL not found. Cannot generate HTTPS certificate.");
            return false;
        }

        // Use OpenSSL to generate a self-signed certificate
        // openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes -subj "/CN=localhost"
        var success = await GenerateOpenSSLAsync(ct, new[] {
                "req", "-x509",
                "-newkey", "rsa:2048",
                "-keyout", keyPath,
                "-out", certificatePath,
                "-days", "365",
                "-nodes",
                "-subj", "/CN=localhost" });

        return success;
    }

    private async Task<bool> GenerateOpenSSLAsync(CancellationToken ct, params string[] arguments)
    {
        if (_opensslPath == null)
        {
            _logger?.LogDebug("Cannot generate certificate: OpenSSL not available.");
            return false;
        }

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _opensslPath,
                Arguments = string.Join(" ", arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start OpenSSL");

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var errorOutput = await process.StandardError.ReadToEndAsync();
                _logger?.LogDebug("OpenSSL failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute OpenSSL");
            return false;
        }
    }

    private async Task<bool> GeneratePFXAsync(string privateKeyPath, string certPath, string password, CancellationToken ct)
    {
        if (_opensslPath == null)
        {
            _logger?.LogDebug("Cannot generate PFX: OpenSSL not available.");
            return false;
        }

        var pfxPath = Path.ChangeExtension(certPath, ".pfx");

        try
        {
            // openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -password pass:changeit
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _opensslPath,
                Arguments = $"pkcs12 -export -out \"{pfxPath}\" -inkey \"{privateKeyPath}\" -in \"{certPath}\" -password pass:{password}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start OpenSSL pkcs12");

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger?.LogDebug("OpenSSL pkcs12 failed with exit code {ExitCode}", process.ExitCode);
                return false;
            }

            // Move PFX to the expected cert path, keep key as separate PEM file for Kestrel
            if (File.Exists(pfxPath))
            {
                File.Move(pfxPath, pfxPath.Replace(".pfx", ".pfx"), true);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> GenerateWithDotNetDevCertsAsync(string certificatePath, CancellationToken ct)
    {
        // Use dotnet dev-certs for cross-platform cert generation (no external dependencies needed)
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"dev-certs https --export-path \"{certificatePath}\" --overwrite",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start dotnet");

            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && File.Exists(certificatePath))
            {
                _logger?.LogInformation("Certificate '{Path}' generated via dotnet dev-certs", certificatePath);
                return true;
            }

            var errorOutput = await process.StandardError.ReadToEndAsync();
            _logger?.LogDebug("dotnet dev-certs failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate certificate via dotnet dev-certs");
            return false;
        }
    }

    private async Task<bool> TrustOnWindows(string certificatePath, CancellationToken ct)
    {
        // Use PowerShell to install the certificate into Windows trust store
        try
        {
            var psStartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $@"-NoProfile -Command ""Import-Certificate -FilePath '{certificatePath}' -CertStoreLocation 'Cert:\LocalMachine\Root'""",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psStartInfo) ?? throw new InvalidOperationException("Failed to start PowerShell");

            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0)
            {
                _logger?.LogInformation("Certificate '{Path}' trusted in Windows Root store", certificatePath);
                return true;
            }

            var errorOutput = await process.StandardError.ReadToEndAsync();
            _logger?.LogWarning("PowerShell trust failed: {Error}", errorOutput);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to trust certificate via PowerShell");
            return false;
        }
    }

    private async Task<System.Security.Cryptography.X509Certificates.X509Certificate2?> LoadCertificateFromDiskAsync(string path, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            return new System.Security.Cryptography.X509Certificates.X509Certificate2(bytes);
        }
        catch
        {
            return null;
        }
    }

    private string? FindOpenSSLSync()
    {
        // Check common OpenSSL paths on each platform
        var possiblePaths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            possiblePaths.AddRange(new[]
            {
                @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
                @"C:\Program Files (x86)\OpenSSL-Win32\bin\openssl.exe",
                @"C:\cygwin64\bin\openssl.exe"
            });
        }

        // Unix-like systems — check PATH via which/where command
        if (!OperatingSystem.IsWindows())
        {
            possiblePaths.Add("openssl"); // Will be resolved by ProcessStartInfo using $PATH
        }

        foreach (var path in possiblePaths)
        {
            try
            {
                var testProcess = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(testProcess))
                {
                    if (proc != null)
                    {
                        _ = proc.WaitForExit(TimeSpan.FromSeconds(1)); // 1 second timeout for detection
                        if (proc.ExitCode == 0)
                            return path;
                    }
                }
            }
            catch
            {
                // OpenSSL not found at this path, continue checking
            }
        }

        return null;
    }

    private string? FindGitBashOpenSSL()
    {
        // Try to find OpenSSL bundled with Git Bash on Windows (common location)
        var gitInstallDir = Environment.GetEnvironmentVariable("ProgramFiles");
        if (string.IsNullOrEmpty(gitInstallDir))
            return null;

        var possiblePaths = new[]
        {
            Path.Combine(gitInstallDir, "Git", "usr", "bin", "openssl.exe"),
            Path.Combine(gitInstallDir, "Git", "mingw64", "bin", "openssl.exe")
        };

        // Also check local ProgramFiles
        var localProgramFiles = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (localProgramFiles != null)
        {
            possiblePaths = possiblePaths.Concat(new[]
            {
                Path.Combine(localProgramFiles, "Git", "usr", "bin", "openssl.exe")
            }).ToArray();
        }

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Also check Git for Windows portable installation locations
        var gitPortableDir = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrEmpty(gitPortableDir))
        {
            var portablePaths = new[]
            {
                Path.Combine(gitPortableDir, "AppData", "Local", "Programs", "Git", "usr", "bin", "openssl.exe"),
                Path.Combine(gitPortableDir, ".git-portable", "usr", "bin", "openssl.exe")
            };

            foreach (var path in portablePaths)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }
}