using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Configurable rate limiting middleware for the local inference server.
/// Limits requests per IP address to prevent abuse and resource exhaustion.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware>? _logger;
    private readonly int _maxRequestsPerWindow;
    private readonly TimeSpan _windowSize;

    public RateLimitMiddleware(
        RequestDelegate next,
        ILogger<RateLimitMiddleware>? logger = null,
        int maxRequestsPerWindow = 60,
        TimeSpan? windowSize = null)
    {
        _next = next;
        _logger = logger;
        _maxRequestsPerWindow = maxRequestsPerWindow;
        _windowSize = windowSize ?? TimeSpan.FromMinutes(1);
    }

    public async Task InvokeAsync(HttpContext context, IRateLimitService rateLimitService)
    {
        var clientIp = GetClientIpAddress(context);
        if (clientIp == null)
        {
            // Cannot determine client IP — allow through to avoid blocking legitimate traffic
            await _next(context);
            return;
        }

        var result = await rateLimitService.TryAcquireAsync(clientIp);
        if (!result.Allowed)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers.Append("Retry-After", ((int)result.WindowRemaining.TotalSeconds).ToString());
            _logger?.LogWarning("Rate limit exceeded for IP '{ClientIp}'. Allowed={Allowed}, Remaining={Remaining}",
                clientIp, result.Allowed, result.RemainingRequests);
            await context.Response.WriteAsJsonAsync(new
            {
                error = "rate_limit_exceeded",
                message = $"Too many requests from your IP. Please retry after {_windowSize.TotalSeconds:F0} seconds.",
                limit = _maxRequestsPerWindow,
                window_seconds = (int)_windowSize.TotalSeconds,
                retry_after_seconds = ((int)result.WindowRemaining.TotalSeconds).ToString()
            });
            return;
        }

        // Track remaining requests in response headers for client awareness
        context.Response.Headers.Append("X-RateLimit-Limit", _maxRequestsPerWindow.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", result.RemainingRequests.ToString());
        context.Response.Headers.Append("X-RateLimit-Reset", ((int)result.WindowRemaining.TotalSeconds + 1).ToString());

        await _next(context);
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Try X-Forwarded-For header first (for proxied requests), then fall back to remote address
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            // Take the first IP in the chain (original client)
            var firstIp = forwardedFor.FirstOrDefault() ?? "";
            return firstIp.Split(',').FirstOrDefault()?.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ??
               context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpConnectionFeature>()?.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Result of a rate limit check.
/// </summary>
public record RateLimitResult(
    bool Allowed,
    int RemainingRequests,
    TimeSpan WindowRemaining);

/// <summary>
/// Thread-safe in-memory rate limiter using the sliding window counter algorithm per IP address.
/// </summary>
public class InMemoryRateLimitService : IRateLimitService, IDisposable
{
    private readonly Dictionary<string, List<long>> _ipWindows = new();
    private readonly object _lockObj = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _windowSize;

    public InMemoryRateLimitService(int maxRequestsPerWindow = 60, TimeSpan? windowSize = null)
    {
        _maxRequests = maxRequestsPerWindow;
        _windowSize = windowSize ?? TimeSpan.FromMinutes(1);
    }

    public async Task<RateLimitResult> TryAcquireAsync(string clientId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long windowStart;

        lock (_lockObj)
        {
            if (!_ipWindows.TryGetValue(clientId, out var window))
                _ipWindows[clientId] = window = new List<long>();

            // Remove expired entries outside the current sliding window (milliseconds)
            window.RemoveAll(ts => ts < now - _windowSize.TotalMilliseconds);
            windowStart = window.Count > 0 ? window.Min() : now;

            // Re-filter to use actual start time of current window
            window.RemoveAll(ts => ts < windowStart);
        }

        lock (_lockObj)
        {
            var elapsedMs = now - windowStart;
            var remainingWindowMs = _windowSize.TotalMilliseconds - elapsedMs;
            var windowRemaining = TimeSpan.FromMilliseconds(remainingWindowMs);

            if (!_ipWindows.TryGetValue(clientId, out var window))
                return new RateLimitResult(true, _maxRequests, _windowSize);

            if (window.Count >= _maxRequests) // Respect configured limit
            {
                return new RateLimitResult(false, 0, windowRemaining);
            }

            window.Add(now);

            return new RateLimitResult(true, Math.Max(0, _maxRequests - window.Count), windowRemaining);
        }
    }

    public void Dispose()
    {
        lock (_lockObj)
        {
            _ipWindows.Clear();
        }
    }
}

/// <summary>
/// Interface for rate limiting service — allows swapping implementations (memory, Redis, etc.).
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Attempts to acquire a request slot for the given client identifier.
    /// Returns Allowed=true if within limit, false otherwise.
    /// </summary>
    Task<RateLimitResult> TryAcquireAsync(string clientId);
}

/// <summary>
/// Extension methods for rate limiting middleware configuration and DI registration.
/// </summary>
public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, int maxRequestsPerWindow = 60, TimeSpan? windowSize = null)
    {
        return app.UseMiddleware<RateLimitMiddleware>(maxRequestsPerWindow, windowSize ?? TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Registers an in-memory rate limiter service for local server use.
    /// For production deployments behind a proxy, consider using Redis-based rate limiting instead.
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, int maxRequestsPerWindow = 60, TimeSpan? windowSize = null)
    {
        return services.AddSingleton<IRateLimitService>(sp =>
            new InMemoryRateLimitService());
    }
}