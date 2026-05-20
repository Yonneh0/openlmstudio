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
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

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
    /// Must call StartWithClassicDesktopLifetime to start the Avalonia application loop and show windows.
    /// </summary>
    public static void Main(string[] args)
    {
        // Ensure unhandled exceptions are logged to a file even in headless/non-UI scenarios.
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = (Exception)e.ExceptionObject;
            WriteFatalError($"Unhandled exception: {ex}");
        };

        System.Diagnostics.Debug.WriteLine("[App] Main called");

        // Build Avalonia app with desktop lifetime — this starts the application loop and shows windows.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Helper to create a desktop app builder — must be called from Main() for Avalonia's platform setup.
    /// The returned AppBuilder must have StartWithClassicDesktopLifetime() called on it to start the application loop.
    /// </summary>
    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();

    /// <summary>
    /// Writes a fatal error to the event log, console (if available), and a file for post-mortem diagnosis.
    /// </summary>
    private static void WriteFatalError(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[App] FATAL ERROR: {message}");

        // Try console output if stderr is not redirected to NUL (GUI-only process)
        try
        {
            if (!Console.IsOutputRedirected && !Console.IsErrorRedirected)
                Console.Error.WriteLine(message);
        }
        catch { /* Ignore */ }

        // Write to a file for later diagnosis
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appData, "OpenLMStudio", "logs");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, $"fatal-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(logPath, message);
            System.Diagnostics.Debug.WriteLine($"[App] Fatal error written to: {logPath}");
        }
        catch (Exception fileEx)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to write fatal log: {fileEx.Message}");
        }
    }

    public override void Initialize()
    {
        System.Diagnostics.Debug.WriteLine("[App] Initialize called");

        AvaloniaXamlLoader.Load(this);

        // Set up DI container for application-wide service resolution
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        try
        {
            System.Diagnostics.Debug.WriteLine("[App] Calling AddApplicationTypes");
            serviceCollection.AddApplicationTypes();

            System.Diagnostics.Debug.WriteLine("[App] Calling AddInfrastructureServices");
            serviceCollection.AddInfrastructureServices();

            ApplicationServices = serviceCollection.BuildServiceProvider();
            System.Diagnostics.Debug.WriteLine("[App] DI container built successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] DI setup failed: {ex}");
            ShowError($"Failed to initialize dependency injection:\n{ex.Message}\n\nInner: {(ex.InnerException?.Message ?? "N/A")}");
            return;
        }

        // Create and show the main window — Avalonia doesn't auto-create a MainWindow like WPF does.
        // We must create it manually here after DI is set up.
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && ApplicationServices != null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App] Creating main window");
                var mainWindow = CreateAndShowMainWindow(ApplicationServices);
                System.Diagnostics.Debug.WriteLine("[App] Showing main window");
                mainWindow.Show();
                System.Diagnostics.Debug.WriteLine("[App] Main window shown successfully");

                // Set MainWindow as the application's startup window for proper shutdown behavior.
                // When the last window closes, Avalonia will automatically shut down the app.
                desktop.MainWindow = mainWindow;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to create/show main window: {ex}");
                ShowError($"Failed to create main window:\n{ex.Message}\n\nInner: {(ex.InnerException?.Message ?? "N/A")}");
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
            System.Diagnostics.Debug.WriteLine($"[App] Showing error: {message}");

            // Try to show via Avalonia Application.Current
            var app = Avalonia.Application.Current;
            if (app != null && app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var parentWindow = desktop.Windows.OfType<Window>().FirstOrDefault();
                System.Diagnostics.Debug.WriteLine($"[App] Found parent window: {parentWindow != null}");

                var errorWindow = new Window
                {
                    Title = "OpenLMStudio - Error",
                    Width = 500,
                    Height = 300,
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
                    },
                    WindowStartupLocation = parentWindow != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
                };

                if (parentWindow != null)
                {
                    errorWindow.Show(parentWindow);
                }
                else
                {
                    // No parent window — show centered on screen (position will be determined by OS)
                    System.Diagnostics.Debug.WriteLine("[App] No parent window, showing without owner");
                    errorWindow.Show();
                }
            }
            else
            {
                // Cannot show UI — write to console and event log instead
                System.Diagnostics.Debug.WriteLine("[App] Cannot show error dialog — no Avalonia application available");
                Console.Error.WriteLine($"OpenLMStudio Error: {message}");

                // Write to a file for later diagnosis
                try
                {
                    var errorLogDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "OpenLMStudio",
                        "logs");
                    if (!string.IsNullOrEmpty(Path.GetDirectoryName(errorLogDir)))
                        Directory.CreateDirectory(errorLogDir);

                    var errorLogPath = Path.Combine(errorLogDir, $"startup-error-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                    File.WriteAllText(errorLogPath, $"Startup Error\n{message}\n\nStack:\n{message}");
                    Console.Error.WriteLine($"Error log written to: {errorLogPath}");
                }
                catch (Exception fileEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Failed to write error log: {fileEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // If we can't show the dialog, just print to console — don't crash the app further
            System.Diagnostics.Debug.WriteLine($"Error showing error: {ex.Message}");
            Console.Error.WriteLine($"OpenLMStudio Error (double-fail): {message}\n\nOriginal error:\n{ex.Message}");

            // Write to a file for later diagnosis
            try
            {
                var errorLogDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OpenLMStudio",
                    "logs");
                if (!string.IsNullOrEmpty(Path.GetDirectoryName(errorLogDir)))
                    Directory.CreateDirectory(errorLogDir);

                var errorLogPath = Path.Combine(errorLogDir, $"startup-double-fail-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.WriteAllText(errorLogPath, $"Startup Error (double fail)\n{message}\n\nOriginal error:\n{ex.Message}");
            }
            catch { /* Ignore */ }
        }
    }
}