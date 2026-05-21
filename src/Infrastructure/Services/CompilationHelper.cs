using OpenLMStudio.Domain.Models.QEMU;
using System.Runtime.InteropServices;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Configuration for a compilation operation.
/// </summary>
public record CompilationConfig(
    ArchitectureType Architecture,
    string SourcePath,
    string OutputPath);

/// <summary>
/// Static helper for generating compilation scripts and checking prerequisites.
/// </summary>
public static class CompilationHelper
{
    /// <summary>
    /// Generates an OS-specific CMake build script for the given configuration.
    /// </summary>
    public static string GetCompileScript(CompilationConfig config)
    {
        var os = GetOSName();

        return os switch
        {
            "win32" => $"""
                cmake -B build -DGGML_XNNPACK=ON -DGGML_CUDA=ON
                cmake --build build --config Release
                """,
            "darwin" => $"""
                cmake -B build \\
                    -DGGML_XNNPACK=ON \\
                    -DGGML_METAL=ON \\
                    -DCMAKE_OSX_ARCHITECTURES=arm64
                cmake --build build --config Release
                """,
            _ => $"""
                cmake -B build \\
                    -DGGML_XNNPACK=ON \\
                    -DGGML_CUDA=ON
                cmake --build build --config Release
                """
        };
    }

    /// <summary>
    /// Gets the OS-specific command to install a compiler toolchain.
    /// </summary>
    public static string GetInstallCompilerCommand(string os)
    {
        return os switch
        {
            "win32" => "winget install --id Microsoft.VisualStudio.2022.BuildTools --accept-package-agreements --quiet",
            "darwin" => "xcode-select --install",
            _ => "sudo apt install build-essential cmake"
        };
    }

    /// <summary>
    /// Checks for required build prerequisites (git, cmake, compiler).
    /// </summary>
    public static async Task<(bool HasGit, bool HasCMake, bool HasCompiler)> CheckPrerequisites(string os)
    {
        var hasGit = await IsCommandAvailable("git").ConfigureAwait(false);
        var hasCMake = await IsCommandAvailable("cmake").ConfigureAwait(false);

        var hasCompiler = os switch
        {
            "win32" => await IsCommandAvailable("cl").ConfigureAwait(false),
            "darwin" => await IsCommandAvailable("clang").ConfigureAwait(false),
            _ => await IsCommandAvailable("gcc").ConfigureAwait(false)
        };

        return (hasGit, hasCMake, hasCompiler);
    }

    /// <summary>
    /// Gets the OS name for script generation.
    /// </summary>
    public static string GetOSName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win32";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "darwin";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        return "linux";
    }

    private static async Task<bool> IsCommandAvailable(string command)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c where {command}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process == null)
                return false;

            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}