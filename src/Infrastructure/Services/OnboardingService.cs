using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the first-run onboarding experience with step navigation and persistence.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private readonly string _stateFilePath;
    private readonly ILogger<OnboardingService>? _logger;
    private int _currentStepIndex;
    private bool _isCompleted;

    public OnboardingService(ILogger<OnboardingService>? logger = null)
    {
        _logger = logger;
        var appDataDir = GetAppDataDirectory();
        _stateFilePath = Path.Combine(appDataDir, "onboarding_complete.flag");
        _isCompleted = File.Exists(_stateFilePath);
        _currentStepIndex = _isCompleted ? IOnboardingService.DefaultSteps.Count - 1 : 0;
    }

    public bool IsCompleted => _isCompleted;

    public int CurrentStepIndex => _isCompleted ? IOnboardingService.DefaultSteps.Count - 1 : _currentStepIndex;

    public OnboardingStep? CurrentStep => _isCompleted ? IOnboardingService.DefaultSteps[IOnboardingService.DefaultSteps.Count - 1] : CurrentStepIndex >= 0 && CurrentStepIndex < IOnboardingService.DefaultSteps.Count ? IOnboardingService.DefaultSteps[CurrentStepIndex] : null;

    public IReadOnlyList<OnboardingStep> GetAllSteps() => IOnboardingService.DefaultSteps;

    public void Next()
    {
        if (_isCompleted) return;
        _currentStepIndex = Math.Min(_currentStepIndex + 1, IOnboardingService.DefaultSteps.Count - 1);
        if (_currentStepIndex >= IOnboardingService.DefaultSteps.Count - 1)
        {
            _isCompleted = true;
            MarkCompleteAsync().Ignore();
        }
    }

    public void Previous()
    {
        if (_isCompleted) return;
        _currentStepIndex = Math.Max(0, _currentStepIndex - 1);
    }

    public void Skip()
    {
        _isCompleted = true;
        MarkCompleteAsync().Ignore();
    }

    public async Task CompleteAsync(CancellationToken ct = default)
    {
        _isCompleted = true;
        await MarkCompleteAsync();
    }

    public void Show()
    {
        _logger?.LogInformation("Onboarding flow shown to user");
    }

    private async Task MarkCompleteAsync()
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

/// <summary>
/// Extension to ignore Task return values (avoid CS4014 warnings).
/// </summary>
internal static class TaskExtensions
{
    public static void Ignore(this System.Threading.Tasks.Task _) { }
}