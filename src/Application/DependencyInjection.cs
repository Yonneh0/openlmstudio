using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Application.Services;
using OpenLMStudio.Application.Services.Agent;
using OpenLMStudio.Domain.Models;

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

        // Agent types
        services.AddTransient<AgentTaskRequest>();
        services.AddTransient<AgentTaskResult>();
        services.AddTransient<AgentToolCallRecord>();
        services.AddTransient<AgentMessageExchange>();
        services.AddTransient<AgentPlan>();

        // Agent Task Management types
        services.AddSingleton<AgentAutoApprovalSettings>();
        services.AddSingleton<AgentBrowserSettings>();
        services.AddSingleton<AgentFocusChainSettings>();
        services.AddSingleton<AgentTaskProgress>();
        services.AddSingleton<AgentTaskChecklistItem>();
        services.AddSingleton<HookResult>();

        // Agent Task Management services
        services.AddTransient<IAgentTaskManager, AgentTaskManager>();
        services.AddTransient<IAgentTaskCheckpointService, AgentTaskCheckpointService>();
        services.AddTransient<IAgentTaskStateService, AgentTaskStateService>();
        services.AddTransient<IAgentTaskProgressService, AgentTaskProgressService>();
        // AgentTaskAutoApprover registered below as singleton (not transient)
        services.AddTransient<IAgentTaskContextManager, AgentTaskContextManager>();
        services.AddTransient<IAgentTaskHookService, AgentTaskHookService>();

        // Context management services
        services.AddTransient<IContextWindowBudgeter, ContextWindowBudgeter>();
        services.AddTransient<ITokenEstimator, TokenEstimator>();
        services.AddTransient<IContextCompressor, ContextCompressor>();
        services.AddTransient<IContextRelevanceEngine, ContextRelevanceEngine>();
        services.AddTransient<IContextManipulator, ContextManipulator>();
        services.AddSingleton<ContextSnapshotManager>();
        services.AddSingleton<SystemPromptGenerator>();

        // Additional agent services
        services.AddSingleton<AgentSessionService>();
        services.AddSingleton<AgentToolExecutor>();
        services.AddSingleton<AgentTaskAutoApprover>();
        services.AddSingleton<AttemptCompletion>();
        services.AddSingleton<NewTask>();

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

        // NOTE: ITaskContextReinjectionService, ITaskContextInheritor, ITaskContextPruner
        // are registered in Infrastructure.DependencyInjection, not here.

        return services;
    }
}
