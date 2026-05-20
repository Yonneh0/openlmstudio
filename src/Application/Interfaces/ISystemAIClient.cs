namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Represents a chunk of streamed text from the System AI.
/// </summary>
public class SseChunk
{
    public string Content { get; }
    public SseChunk(string content) => Content = content;
}

/// <summary>
/// Represents a completion signal from the System AI.
/// </summary>
public class SseDone;

/// <summary>
/// Client for the System AI (llama.cpp) inference engine.
/// Communicates via stdin/stdout JSON protocol, not HTTP.
/// </summary>
public interface ISystemAIClient : IDisposable
{
    /// <summary>
    /// Event fired when a text chunk is received.
    /// </summary>
    event EventHandler<SseChunk>? OnChunk;

    /// <summary>
    /// Event fired when the response is complete.
    /// </summary>
    event EventHandler<SseDone>? OnDone;

    /// <summary>
    /// Event fired when an error occurs.
    /// </summary>
    event EventHandler<string>? OnError;

    /// <summary>
    /// Starts the llama-server process with the configured model.
    /// </summary>
    Task<bool> StartAsync();

    /// <summary>
    /// Sends a message to the System AI and streams the response.
    /// </summary>
    Task<string?> SendMessageAsync(string message, string? compressedContext = null);

    /// <summary>
    /// Stops the llama-server process and cleans up resources.
    /// </summary>
    Task StopAsync();
}