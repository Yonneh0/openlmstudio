using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Services;
using OpenLMStudio.Desktop.Services;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Desktop-specific DI registrations.
/// Call AddDesktopServices() from App.axaml.cs before building the service provider.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // Markdown renderer: converts markdown (via Markdig) to Avalonia-compatible markup
        services.AddSingleton<IMarkdownRenderer, AvaloniaMarkdownRenderer>();

        return services;
    }
}