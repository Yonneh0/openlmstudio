// Avalonia Application entry point — initializes DI container and registers all services

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for App.axaml.
/// Initializes the DI service provider and configures application-wide services including logging,
/// conversation management, device monitoring, model repository, download manager, server service,
/// and chat completion service via IChatCompletionService interface.
/// </summary>
public partial class App : Avalonia.Application
{
    /// <summary>
    /// Gets the application's DI service provider after Initialize completes.
    /// Available for querying services from any component that needs them.
    /// </summary>
    public static IServiceProvider? ApplicationServices { get; private set; }

    /// <summary>
    /// Entry point for the application. Called by the platform-specific host before the window is shown.
    /// </summary>
    public static void Main(string[] args)
    {
        BuildAvaloniaApp();
    }

    /// <summary>
    /// Helper to create a desktop app builder — must be called from Main() for Avalonia's platform setup.
    /// </summary>
    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Set up DI container for application-wide service resolution
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        serviceCollection.AddApplicationTypes();
        serviceCollection.AddInfrastructureServices();
        ApplicationServices = serviceCollection.BuildServiceProvider();

        // Create and show the main window — Avalonia doesn't auto-create a MainWindow like WPF does.
        // We must create it manually here after DI is set up.
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var mainWindow = CreateAndShowMainWindow(ApplicationServices!);
                mainWindow.Show();
                desktop.ShutdownRequested += (_, _) => mainWindow?.Close();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to create main window:\n{ex.Message}");
            }
        }
    }

    /// <summary>
    /// Creates the main window using DI-resolved services.
    /// </summary>
    private static MainWindow CreateAndShowMainWindow(IServiceProvider serviceProvider)
    {
        try
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            // Resolve and pass infrastructure dependencies to MainWindow constructor
            var conversationManager = serviceProvider.GetRequiredService<IConversationManager>();
            var serverService = serviceProvider.GetRequiredService<IServerService>();
            var modelRepository = serviceProvider.GetRequiredService<IModelRepository>();

            return new MainWindow(
                loggerFactory.CreateLogger<MainWindow>(),
                conversationManager,
                serverService,
                modelRepository);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to create main window:\n{ex.Message}");
            throw; // Re-throw — application cannot start without the main window
        }
    }

    /// <summary>
    /// Shows an error dialog. In Avalonia, we use a simple Window with content.
    /// </summary>
    private static void ShowError(string message)
    {
        try
        {
            // Avalonia doesn't have a global screen detection API for centering.
            // Use the parent window if available, otherwise let the OS position it.
            var owner = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parentWindow = owner?.Windows.FirstOrDefault();

            var errorWindow = new Window
            {
                Title = "OpenLMStudio - Error",
                Width = 400,
                Height = 250,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 37, 37, 41)),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                        Padding = new Thickness(20),
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            if (parentWindow != null)
                errorWindow.ShowDialog(parentWindow);
            else
                errorWindow.Show();
        }
        catch
        {
            // If we can't show the dialog, just print to console — don't crash the app further
            System.Diagnostics.Debug.WriteLine($"Error: {message}");
        }
    }
}