using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Application;

/// <summary>
/// Service collection extensions for the application layer.
/// Provides dependency injection configuration for application-layer types only.
/// Infrastructure implementation registrations are handled in Desktop or Infrastructure projects.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all application-layer DTOs and transient services with the dependency injection container.
    /// Domain interface implementations should be registered separately via AddInfrastructureServices.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddApplicationTypes(this IServiceCollection services)
    {
        // Chat request/response DTOs (transient - created per operation)
        services.AddTransient<ChatCompletionRequest>();
        services.AddTransient<ChatMessage>();
        services.AddTransient<ChatChoice>();
        services.AddTransient<ChatCompletionResponse>();

        // Streaming event handler for real-time token delivery
        services.AddTransient<StreamingEventHandler>();

        return services;
    }

    /// <summary>
    /// Registers the OpenAI-compatible API endpoint handler with the application pipeline.
    /// This method should be called on IApplicationBuilder during app configuration.
    /// </summary>
    public static void ConfigureOpenApiEndpoints(this IServiceCollection services)
    {
        // The endpoint handler is a static class - no DI registration needed
        // It will be invoked directly via the application builder extension method
        
        // Register any dependencies the OpenAPI endpoints need to resolve from DI
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>();
        
        return;
    }

    /// <summary>
    /// Convenience method to register all Application and Infrastructure services at once.
    /// </summary>
    public static IServiceCollection AddOpenLMStudioServices(this IServiceCollection services)
    {
        services.AddApplicationTypes();

        return services;
    }
}