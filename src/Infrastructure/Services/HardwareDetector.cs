using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Hardware detection for recommended backend selection.
/// </summary>
public class HardwareDetector
{
    private readonly ILogger<HardwareDetector>? _logger;

    public HardwareDetector(ILogger<HardwareDetector> logger)
    {
        _logger = logger;
    }

    public async Task<(string Platform, string? Gpu, int RamGB)> DetectAsync()
    {
        var platform = DetectPlatform();
        string? gpu = null;

        if (platform == "win32")
        {
            try
            {
                gpu = await RunCommandAsync("wmic", "path win32_VideoController get name", 5000);
                gpu = gpu?.Split('\n').FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && !s.Contains("Name"))?.Trim();
            }
            catch { }
        }

        int ramGB = DetectRamGB(platform);

        _logger?.LogDebug("Detected hardware: platform={Platform}, gpu={Gpu}, ram={Ram}GB", platform, gpu, ramGB);
        return (platform, gpu, ramGB);
    }

    public string GetRecommendedBackend(string platform, string? gpu)
    {
        if (platform == "darwin") return "metal";
        if (gpu != null && gpu.ToLower().Contains("nvidia")) return "cuda";
        if (gpu != null) return "vulkan";
        return "cpu";
    }

    private static string DetectPlatform()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S)
            return "win32";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("MACOS")))
            return "darwin";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        return "unknown";
    }

    private static int DetectRamGB(string platform)
    {
        try
        {
            if (platform == "win32")
            {
                var total = RunCommandSync("wmic", "os get totalvisiblememorysize");
                if (int.TryParse(total?.Split('\n').FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && !s.Contains("TotalVisibleMemorySize"))?.Split('=')[1], out var val))
                    return val / (1024 * 1024);
            }
            else if (platform == "darwin")
            {
                var outp = RunCommandSync("sysctl", "-n hw.memsize");
                if (int.TryParse(outp, out var mem))
                    return mem / (1024 * 1024 * 1024);
            }
            else
            {
                var line = File.ReadAllText("/proc/meminfo").Split('\n').FirstOrDefault(l => l.StartsWith("MemTotal"));
                if (int.TryParse(line?.Split(':')[1].Trim().Split(' ')[0], out var kb))
                    return kb / (1024 * 1024);
            }
        }
        catch { }

        return 16; // default
    }

    private static string? RunCommandSync(string cmd, string args)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            proc!.WaitForExit(5000);
            return proc.StandardOutput.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> RunCommandAsync(string cmd, string args, int timeout = 5000)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            await proc!.WaitForExitAsync(CancellationToken.None);
            if (!proc.HasExited)
            {
                proc.Kill();
                return null;
            }
            return await proc.StandardOutput.ReadToEndAsync();
        }
        catch
        {
            return null;
        }
    }
}