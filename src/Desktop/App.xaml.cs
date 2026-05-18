// WPF Application entry point — initializes DI container and registers all services

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Infrastructure;
using OpenLMStudio.Infrastructure.Services;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for App.xaml.
/// Initializes the DI service provider and configures application-wide services including logging,
/// conversation management, device monitoring, model repository, download manager, server service,
/// and chat completion service via IChatCompletionService interface.
/// </summary>
public partial class App : global::System.Windows.Application
{
    /// <summary>
    /// Gets the application's DI service provider after OnStartup completes.
    /// Available for querying services from any component that needs them.
    /// </summary>
    public static IServiceProvider? ApplicationServices { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Create DI container with all application services registered
            var serviceCollection = new ServiceCollection();

            // Register logging
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            // Register application-layer types (DTOs) and infrastructure implementations
            serviceCollection.AddApplicationTypes();
            serviceCollection.AddInfrastructureServices();

            // Build the service provider — store it on Application for later resolution
            var serviceProvider = serviceCollection.BuildServiceProvider();
            ApplicationServices = serviceProvider;

            var mainWindow = CreateAndShowMainWindow(serviceProvider);
            if (mainWindow != null && global::System.Windows.Application.Current != null)
            {
                global::System.Windows.Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize application:\n{ex.Message}", "OpenLMStudio - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Creates and shows the main window using DI-resolved services.
    /// </summary>
    private static MainWindow CreateAndShowMainWindow(IServiceProvider serviceProvider)
    {
        try
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("App");

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
            MessageBox.Show($"Failed to create main window:\n{ex.Message}", "OpenLMStudio - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return null!; // Will cause Shutdown(1) in caller
        }
    }
}