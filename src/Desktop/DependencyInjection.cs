using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.ContextCompression;
using OpenLMStudio.Domain.Models.Pingu;
using OpenLMStudio.Infrastructure.Services;
using OpenLMStudio.Infrastructure.Models;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Registers infrastructure services with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Existing services
        services.AddSingleton<IServerService, ServerService>();
        services.AddSingleton<IModelRepository, ModelRepository>();
        services.AddSingleton<IChatCompletionService, ChatCompletionService>();
        services.AddSingleton<IChatContextManager, ChatContextManager>();
        services.AddSingleton<IContextWindowBudgeter, ContextWindowBudgeter>();
        services.AddSingleton<IPinguStore, PinguStore>();
        services.AddSingleton<IWindowSettings, WindowSettings>();
        services.AddSingleton<IEngineLogger, EngineLogger>();
        services.AddSingleton<IGamesPanelService, GamesPanelService>();
        services.AddSingleton<PanelService>();
        services.AddSingleton<TabService>();
        services.AddSingleton<AvaloniaMarkdownRenderer>();
        services.AddSingleton<DependencyInjection>();

        // New llama.cpp services
        services.AddSingleton<BinaryRegistry>();
        services.AddSingleton<EngineBinaryDownloader>();
        services.AddSingleton<GgufParser>();
        services.AddSingleton<ModelRecommendationService>();
        services.AddSingleton<GgufModelDownloader>();
        services.AddSingleton<LlamaServerHelpParser>();
        services.AddSingleton<LogViewerService>();
        services.AddSingleton<EngineConfigService>();
        services.AddSingleton<MainAIManager>();
        services.AddSingleton<SystemAIManager>();

        return services;
    }
}