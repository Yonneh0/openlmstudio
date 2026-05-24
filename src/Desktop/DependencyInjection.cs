using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.ContextCompression;
using OpenLMStudio.Domain.Models.Pingu;
using InfraServices = OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Registers infrastructure services with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Existing services (Infrastructure layer)
        services.AddSingleton<IServerService, InfraServices.ServerService>();
        services.AddSingleton<IModelRepository, InfraServices.JsonModelRepository>();
        services.AddSingleton<IChatCompletionService, InfraServices.LlamaCppChatCompletionService>();
        services.AddSingleton<IChatContextManager, InfraServices.ChatContextManager>();
        services.AddSingleton<IContextWindowBudgeter, InfraServices.ContextWindowBudgeter>();
        services.AddSingleton<IPinguStore, InfraServices.PinguStore>();
        services.AddSingleton<IWindowSettings, InfraServices.WindowSettingsService>();
        services.AddSingleton<IEngineLogger, InfraServices.EngineLogger>();
        services.AddSingleton<IGamesPanel, GamesPanelService>();
        services.AddSingleton<PanelService>();
        services.AddSingleton<TabService>();
        services.AddSingleton<InfraServices.AvaloniaMarkdownRenderer>();

        // New llama.cpp services
        services.AddSingleton<InfraServices.BinaryRegistry>();
        services.AddSingleton<InfraServices.EngineBinaryDownloader>();
        services.AddSingleton<InfraServices.GgufParser>();
        services.AddSingleton<InfraServices.ModelRecommendationService>();
        services.AddSingleton<InfraServices.GgufModelDownloader>();
        services.AddSingleton<InfraServices.LlamaServerHelpParser>();
        services.AddSingleton<InfraServices.LogViewerService>();
        services.AddSingleton<InfraServices.EngineConfigService>();
        services.AddSingleton<InfraServices.MainAIManager>();
        services.AddSingleton<InfraServices.SystemAIManager>();

        // ConversationManager (required by App.axaml.cs)
        services.AddSingleton<IConversationManager, InfraServices.FileConversationManager>();

        return services;
    }

    /// <summary>
    /// Registers Desktop-layer services with the DI container.
    /// </summary>
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        return services;
    }
}
