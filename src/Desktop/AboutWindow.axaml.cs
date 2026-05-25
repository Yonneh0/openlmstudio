using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for AboutWindow.axaml.
/// Displays application version, runtime info, tech stack, and links.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        PopulateInfo();
    }

    private void PopulateInfo()
    {
        // Version from assembly
        var assembly = typeof(AboutWindow).Assembly;
        var version = assembly.GetName().Version;
        var versionStr = version?.ToString() ?? "1.0.0";
        VersionText.Text = $"v{versionStr}";
        AppVersionText.Text = versionStr;

        // Git commit (set at build time by MSBuild)
        var commit = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+')
            .FirstOrDefault();

        if (string.IsNullOrEmpty(commit))
        {
            // Fallback: try to get from git
            commit = GetGitCommit();
        }

        CommitText.Text = commit ?? "unknown";
        BranchText.Text = GetGitBranch() ?? "unknown";

        // Build type
        var configuration = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration ?? "Release";
        BuildTypeText.Text = configuration;

        // Runtime info
        RuntimeText.Text = $".NET {Environment.Version}";
        OsText.Text = GetOSDescription();
        ArchitectureText.Text = RuntimeInformation.OSArchitecture.ToString();
    }

    private static string GetOSDescription()
    {
        return RuntimeInformation.OSDescription ?? "Unknown";
    }

    private static string GetGitCommit()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadLine();
                return output?.Trim();
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static string GetGitBranch()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadLine();
                return output?.Trim();
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Close button is in the title bar
        if (CloseButton != null)
            CloseButton.Click += OnCloseClicked;
    }
}
