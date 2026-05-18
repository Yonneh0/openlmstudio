using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Handles mapping and routing for OpenAI-compatible API endpoints.
/// Wraps the existing IChatCompletionService to provide HTTP-accessible APIs.
/// Image generation (/v1/images/generations) and embedding (/v1/embeddings) endpoints
/// are registered in ServerService.ConfigureOpenApiEndpoints() instead to avoid duplicate routes.
/// </summary>
public static class OpenApiEndpointHandler
{
    /// <summary>
    /// Configures the OpenAI-compatible endpoint routes on the application builder.
    /// Only registers /health, /v1/models/list, and /v1/chat/completions here; other endpoints are in ServerService.ConfigureOpenApiEndpoints().
    /// </summary>
    public static void ConfigureOpenApiEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapGet("/health", () => Results.Json(new { status = "ok" }));

        // OpenAI-compatible: /v1/models/list - List available models (GET per OpenAI spec)
        app.MapGet("/v1/models/list", async (IModelRepository repo, HttpContext context) =>
        {
            var models = await repo.ListModelsAsync();

            var modelInfos = new List<object>();
            foreach (var model in models)
            {
                modelInfos.Add(new
                {
                    id = model.Id.ToString(),
                    obj = "model",
                    owned_by = "local",
                    display_name = model.Name,
                    quantization = model.Quantization ?? "N/A",
                    is_active = model.IsActive,
                    memory_usage_bytes = 0L
                });
            }

            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(new
            {
                obj = "list",
                data = modelInfos
            });
        });

        // OpenAI-compatible: /v1/chat/completions - Main chat endpoint (with multi-engine routing)
        app.MapPost("/v1/chat/completions", async (IChatCompletionService service, HttpContext context) =>
        {
            try
            {
                var request = await context.Request.ReadFromJsonAsync<OpenApiRequest>();

                if (request == null || string.IsNullOrEmpty(request.Model))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new ErrorResponse
                    {
                        message = "Model identifier is required.",
                        code = "missing_model",
                        type = "invalid_request_error"
                    });
                    return;
                }

                // Route based on model type if Type field is specified (multi-engine routing)
                var modelType = request.Type?.ToLower() ?? null;

                // If Type specifies image generation, route to /v1/images/generations instead
                if (modelType == "image" || modelType == "diffusion")
                {
                    context.Response.StatusCode = 405;
                    await context.Response.WriteAsJsonAsync(new ErrorResponse
                    {
                        message = "Image generation requires using /v1/images/generations endpoint.",
                        code = "wrong_endpoint",
                        type = "endpoint_not_available"
                    });
                    return;
                }

                // If Type specifies embedding, route to /v1/embeddings instead
                if (modelType == "embedding")
                {
                    context.Response.StatusCode = 405;
                    await context.Response.WriteAsJsonAsync(new ErrorResponse
                    {
                        message = "Embedding generation requires using /v1/embeddings endpoint.",
                        code = "wrong_endpoint",
                        type = "endpoint_not_available"
                    });
                    return;
                }

                // Default: text generation (no Type field or explicit "text") — use chat completion service
                var messages = new List<Message>();
                foreach (var msg in request.Messages ?? Array.Empty<OpenApiMessage>())
                {
                    messages.Add(new Message
                    {
                        Role = MapRole(msg.role),
                        Content = msg.content ?? "",
                        CreatedAt = DateTime.UtcNow,
                        Id = Guid.NewGuid()
                    });
                }

                var chatRequest = new ChatRequest(
                    request.Model,
                    messages,
                    (double?)(request.Temperature ?? 0.7),
                    request.MaxTokens > 0 ? (int?)request.MaxTokens : null,
                    (double?)(request.TopP ?? 1.0),
                    false);

                // Non-streaming response - use GetCompletionAsync from IChatCompletionService interface
                var responseChoice = await service.GetCompletionAsync(chatRequest);

                var contentLength = !string.IsNullOrEmpty(responseChoice.Message.Content)
                    ? responseChoice.Message.Content.Length
                    : 0;

                var response = new OpenApiResponse
                {
                    id = Guid.NewGuid().ToString("n"),
                    obj = "chat.completion",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    model = request.Model,
                    choices = new[]
                    {
                        new OpenApiChoice
                        {
                            index = 0,
                            message = new OpenApiMessage
                            {
                                role = "assistant",
                                content = responseChoice.Message.Content
                            } as OpenApiMessage,
                            finish_reason = responseChoice.FinishReason ?? "stop"
                        }
                    },
                    usage = new OpenApiUsage
                    {
                        prompt_tokens = messages.Sum(m => m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content)),
                        completion_tokens = !string.IsNullOrEmpty(responseChoice.Message.Content)
                            ? (responseChoice.Message.TokenCount > 0 ? responseChoice.Message.TokenCount : EstimateTokenCount(responseChoice.Message.Content))
                            : 0,
                        total_tokens = messages.Sum(m => m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content)) +
                                      (!string.IsNullOrEmpty(responseChoice.Message.Content)
                                          ? (responseChoice.Message.TokenCount > 0 ? responseChoice.Message.TokenCount : EstimateTokenCount(responseChoice.Message.Content))
                                          : 0)
                    }
                };

                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    message = ex.Message,
                    code = "internal_error",
                    type = "server_error"
                });
            }
        });
    }

    /// <summary>
    /// Maps OpenAI role string to our MessageRole enum.
    /// </summary>
    private static MessageRole MapRole(string? role) => (role ?? "user").ToLower() switch
    {
        "user" => MessageRole.User,
        "assistant" => MessageRole.Assistant,
        "system" => MessageRole.System,
        _ => MessageRole.User
    };

    /// <summary>
    /// Standardized token counting method using consistent estimation: ~1 token per 4 characters for English.
    /// </summary>
    private static int EstimateTokenCount(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}

/// <summary>
/// OpenAI-compatible request format (for HTTP endpoint deserialization).
/// </summary>
internal record OpenApiRequest
{
    public string? Model { get; init; }
    public IEnumerable<OpenApiMessage>? Messages { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public float? TopP { get; init; }
    public bool? Stream { get; init; }

    /// <summary>
    /// Model type for multi-engine routing (e.g., "text", "image"). Defaults to null.
    /// </summary>
    public string? Type { get; init; }
}

/// <summary>
/// OpenAI-compatible message format (for HTTP endpoint deserialization).
/// </summary>
internal record OpenApiMessage
{
    public string? role { get; init; }
    public string? content { get; init; }
}

/// <summary>
/// OpenAI-compatible response format (for HTTP endpoint serialization).
/// </summary>
internal record OpenApiResponse
{
    public required string id { get; init; }
    public required string obj { get; init; }
    public required long created { get; init; }
    public required string model { get; init; }
    public required IEnumerable<OpenApiChoice> choices { get; init; }
    public required OpenApiUsage usage { get; init; }
}

/// <summary>
/// OpenAI-compatible choice format (for HTTP endpoint serialization).
/// </summary>
internal record OpenApiChoice
{
    public required int index { get; init; }
    public required OpenApiMessage message { get; init; }
    public string? finish_reason { get; set; }
}

/// <summary>
/// OpenAI-compatible usage format (for HTTP endpoint serialization).
/// </summary>
internal record OpenApiUsage
{
    public required int prompt_tokens { get; init; }
    public required int completion_tokens { get; init; }
    public required int total_tokens { get; init; }
}

/// <summary>
/// Error response format (for HTTP endpoint serialization).
/// </summary>
internal record ErrorResponse
{
    public string obj => "error";
    public required string message { get; set; }
    public string code { get; set; } = "";
    public string type { get; set; } = "";
}