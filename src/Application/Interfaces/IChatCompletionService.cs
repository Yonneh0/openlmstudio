using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a request to generate a chat completion from the LLM.
/// </summary>
public record ChatRequest(
    string ModelId,
    List<Message> Messages,
    double? Temperature = 0.7,
    int? MaxTokens = null,
    double? TopP = 1.0,
    bool Stream = false
);

/// <summary>
/// Represents a single choice within the chat completion response.
/// </summary>
public record ChatResponseChoice(
    Message Message,
    string? FinishReason = null,
    int? Index = 0
);

// Note: ChatCompletionResponse is defined in Application.Types namespace (ChatResponseTypes.cs) to avoid duplicate type conflicts.

/// <summary>
/// Interface for a service that handles LLM chat completion requests.
/// </summary>
public interface IChatCompletionService
{
    /// <summary>
    /// Sends a chat request and returns the AI response.
    /// </summary>
    /// <param name="request">The chat completion request parameters.</param>
    /// <returns>Awaitable task returning the model's response choice.</returns>
    Task<ChatResponseChoice> GetCompletionAsync(ChatRequest request);

    /// <summary>
    /// Sends a streaming chat request and yields responses token-by-token.
    /// </summary>
    /// <param name="request">The chat completion request parameters.</param>
    /// <returns>Awaitable enumerable of partial response chunks.</returns>
    IAsyncEnumerable<string> GetStreamingCompletionAsync(ChatRequest request);
}