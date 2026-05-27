using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
    private readonly ILogger<AboutWindow>? _logger = null!;

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
        // AppVersionText not in XAML — skip; VersionText already shows v{versionStr}
        // AppVersionText.Text = versionStr;

        // Git info from AssemblyInfo
        CommitText.Text = GitInfo.Commit;
        BranchText.Text = GitInfo.Branch;

        // Dirty indicator — "⦿" when dirty, "●" when clean
        DirtyText.Text = string.Equals(GitInfo.Dirty, "true", StringComparison.OrdinalIgnoreCase)
            ? "⦿"
            : "●";
        DirtyText.Foreground = string.Equals(GitInfo.Dirty, "true", StringComparison.OrdinalIgnoreCase)
            ? (Avalonia.Media.ISolidColorBrush)(this.FindResource("AccentOrange") ?? Avalonia.Media.Brushes.Orange)
            : (Avalonia.Media.ISolidColorBrush)(this.FindResource("AccentGreen") ?? Avalonia.Media.Brushes.Green);

        // Build type
        var configuration = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration ?? "Release";
        BuildTypeText.Text = configuration;

        // Runtime info
        RuntimeText.Text = $".NET {Environment.Version}";
        OsText.Text = RuntimeInformation.OSDescription ?? "Unknown";
        ArchitectureText.Text = RuntimeInformation.OSArchitecture.ToString();
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnVersionBadgeClicked(object? sender, PointerPressedEventArgs e)
    {
        // Show the full git log popup
        var log = GetGitLog(7);
        var sb = new StringBuilder();

        sb.Append($"OpenLMStudio {GitInfo.FullName}");
        if (!string.Equals(GitInfo.Dirty, "true", StringComparison.OrdinalIgnoreCase))
            sb.Append(" (clean)");
        sb.AppendLine();
        sb.AppendLine("Recent commits:");
        foreach (var line in log.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            sb.AppendLine(line.Trim());
        }

        // Create a popup to show the git log
        var popup = new Popup
        {
            Width = 600,
            Height = 400,
            Placement = PlacementMode.Center,
            HorizontalOffset = -126.0,
            VerticalOffset = -44.0,
            IsOpen = true,
            PlacementTarget = this,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 41)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 51, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Child = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new TextBlock
                    {
                        Text = sb.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(8)
                    }
                }
            }
        };

        // Auto-close popup when clicking outside
        popup.Closed += (s, _) => popup.Close();
    }

    private string GetGitLog(int count)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"log -{count} --pretty=format:'%h · %s'",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join("\n", lines.Select(l => l.Trim()));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get git log");
        }

        return string.Empty;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
    }
}
