using System.IO;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Persists onboarding completion state to disk using the appdata directory.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private readonly string _stateFilePath;
    private readonly ILogger<OnboardingService>? _logger;

    public OnboardingService(ILogger<OnboardingService>? logger = null)
    {
        _logger = logger;
        var appDataDir = GetAppDataDirectory();
        _stateFilePath = Path.Combine(appDataDir, "onboarding_complete.flag");
    }

    public bool IsComplete => File.Exists(_stateFilePath);

    public async Task<bool> ShowOnboardingAsync()
    {
        // In a real implementation, this would show a modal onboarding dialog
        // For now, we just mark it as complete
        await MarkCompleteAsync();
        return true;
    }

    public async Task MarkCompleteAsync()
    {
        try
        {
            var appDataDir = GetAppDataDirectory();
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(_stateFilePath, DateTime.UtcNow.ToString("o"));
            _logger?.LogInformation("Onboarding marked as complete");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to mark onboarding as complete");
        }
    }

    private static string GetAppDataDirectory()
    {
        var appName = "OpenLMStudio";
        var platform = Environment.OSVersion.Platform;

        return platform switch
        {
            PlatformID.Win32NT => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName),
            PlatformID.MacOSX => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName),
            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName),
        };
    }
}