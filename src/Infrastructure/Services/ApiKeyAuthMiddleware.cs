using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Middleware that validates API key authentication on protected endpoints.
/// Skips auth for unauthenticated endpoints like health checks and model listings.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware>? _logger;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint paths that do NOT require API key authentication.
    /// </summary>
    private static readonly HashSet<string> UnauthenticatedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/v1/health",
        "/v1/models",
        "/v1/models/image/list",
        "/v1/models/vae/list",
        "/v1/models/lora/list",
        "/v1/models/embedding/list",
        // Image generation endpoints — also unauthenticated (same as /v1/images/generations)
        "/v1/images/inpainting",
        "/v1/images/outpainting"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for unauthenticated paths
        if (UnauthenticatedPaths.Contains(context.Request.Path.Value ?? ""))
        {
            await _next(context);
            return;
        }

        // Check for API key in Authorization header or x-api-key query parameter
        string? apiKey = context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader)
            ? apiKeyHeader.FirstOrDefault()
            : null;

        if (string.IsNullOrEmpty(apiKey))
        {
            try
            {
                // Check for API key in query parameters as a fallback
                var queryApiKey = context.Request.Query["api_key"].FirstOrDefault();
                if (!string.IsNullOrEmpty(queryApiKey))
                {
                    apiKey = queryApiKey;
                }
            }
            catch
            {
                // Ignore query parsing errors — they'll be caught later in the pipeline
            }

            _logger?.LogWarning("Missing API key for endpoint '{Endpoint}'", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "missing_api_key",
                message = "An API key is required to access this endpoint. Provide it via the 'X-Api-Key' header or 'api_key' query parameter."
            });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for API key authentication middleware configuration and DI registration.
/// </summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the application pipeline.
    /// Must be called before endpoint registrations that require auth.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyAuthMiddleware>();
    }

    /// <summary>
    /// Registers the API key authentication middleware with a logger instance.
    /// Useful for adding to the DI container before endpoint registration.
    /// </summary>
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services)
    {
        return services.AddSingleton<ApiKeyAuthMiddleware>();
    }
}