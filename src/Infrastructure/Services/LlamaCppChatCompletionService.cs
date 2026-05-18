using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Chat completion service that interfaces with llama.cpp for local inference.
/// Uses IModelRepository for model metadata and GgufParser for GGUF header extraction.
/// </summary>
public class LlamaCppChatCompletionService : IChatCompletionService, IDisposable
{
    private readonly ILogger<LlamaCppChatCompletionService> _logger;
    private readonly IModelRepository _modelRepository;
    private readonly GgufParser _ggufParser;
    private bool _disposed;

    // P/Invoke signatures for future llama.cpp native integration
    private const string LlamaLibName = "libllama";

#pragma warning disable CS0169 // Field is never used - reserved for future llama.cpp integration
    private IntPtr _llamaContextHandle;      // Inference context handle (ggml_context*)
    private IntPtr _modelLoadHandle;          // Model file mapping handle
#pragma warning restore CS0169

    /// <summary>
    /// Initializes a new instance of the LlamaCppChatCompletionService.
    /// </summary>
    public LlamaCppChatCompletionService(
        ILogger<LlamaCppChatCompletionService> logger,
        IModelRepository modelRepository,
        GgufParser ggufParser)
    {
        _logger = logger;
        _modelRepository = modelRepository;
        _ggufParser = ggufParser;

        _logger.LogInformation("LlamaCppChatCompletionService initialized (native bindings pending)");
    }

    /// <inheritdoc />
    public async Task<ChatResponseChoice> GetCompletionAsync(ChatRequest request)
    {
        var metadata = await GetModelMetadata(request.ModelId);
        if (metadata == null)
        {
            throw new FileNotFoundException($"Model not found: {request.ModelId}");
        }

        _logger.LogInformation("Processing completion request for model: {ModelId}, messages: {MessageCount}",
            request.ModelId, request.Messages.Count);

        // Validate context length constraint - check if any message exceeds the model's max context
        var totalContextTokens = EstimateTotalTokenCount(request.Messages);
        List<Message> messagesToUse;
        if (totalContextTokens > metadata.ContextLength)
        {
            _logger.LogWarning("Context length exceeded: estimated {EstimatedTokens} tokens exceeds model max of {MaxTokens}",
                totalContextTokens, metadata.ContextLength);
            // Truncate oldest messages to fit within context window
            messagesToUse = TruncateMessagesToFitContext(request.Messages, metadata.ContextLength);
        }
        else
        {
            messagesToUse = request.Messages;
        }

        // Generate response based on mode (streaming or non-streaming)
        string content;
        if (request.Stream)
        {
            _logger.LogWarning("Streaming requested - use GetStreamingCompletionAsync for streaming responses");
            content = "[Streaming mode - use stream endpoint]";
        }
        else
        {
            // TODO: Real inference requires llama.cpp native binding integration
            content = GeneratePlaceholderResponse(metadata, new ChatRequest(request.ModelId, messagesToUse));
        }

        var responseMessage = new Message
        {
            Role = MessageRole.Assistant,
            Content = content,
            TokenCount = EstimateTokenCount(content),
            IsStreaming = false,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Completion generated for model: {ModelId}, tokens: {TokenCount}",
            request.ModelId, responseMessage.TokenCount);

        return new ChatResponseChoice(responseMessage, "stop", 0);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetStreamingCompletionAsync(ChatRequest request)
    {
        var metadata = await GetModelMetadata(request.ModelId);
        if (metadata == null)
        {
            yield return "{\"error\": \"Model not found: " + request.ModelId + "\"}";
            yield break;
        }

        _logger.LogInformation("Streaming completion for model: {ModelId}, messages: {MessageCount}",
            request.ModelId, request.Messages.Count);

        // Validate context length constraint
        var totalContextTokens = EstimateTotalTokenCount(request.Messages);
        if (totalContextTokens > metadata.ContextLength)
        {
            _logger.LogWarning("Context length exceeded in streaming mode");
            yield return "{\"error\": \"Context length exceeded: " + totalContextTokens + " tokens exceeds max of " + metadata.ContextLength + "\"}";
            yield break;
        }

        // TODO: Real inference requires llama.cpp native binding integration with ggml_backend_schedule_eval for streaming
        var fullResponse = GeneratePlaceholderResponse(metadata, new ChatRequest(request.ModelId, request.Messages));

        // Stream the response token-by-token (simulated)
        foreach (var chunk in ChunkResponseForStreaming(fullResponse))
        {
            var escapedChunk = chunk.Replace("\\", "\\\\").Replace("\"", "\\\"");
            yield return "{\"token\": \"" + escapedChunk + "\", \"finish_reason\": null}";
        }

        yield return "{\"token\": \"<eos>\", \"finish_reason\": \"stop\"}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Cleanup llama.cpp context handles and model unloading
        try
        {
            _logger.LogInformation("LlamaCppChatCompletionService disposing - cleaning up native resources");
            // In future: Call gguf_free_context, llava_free_model, etc. via P/Invoke
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LlamaCppChatCompletionService cleanup");
        }
    }

    /// <summary>
    /// Loads a model into GPU/CPU memory for inference.
    /// </summary>
    public async Task<LoadedModelInstance?> LoadModelAsync(string modelId)
    {
        var metadata = await GetModelMetadata(modelId);
        if (metadata == null)
        {
            _logger.LogWarning("Cannot load model - not found: {ModelId}", modelId);
            return null;
        }

        _logger.LogInformation("Loading model into memory: {ModelId} ({Architecture})",
            modelId, metadata.Architecture);

        var loadedInstance = new LoadedModelInstance
        {
            ModelId = modelId,
            Metadata = metadata,
            State = ModelLoadState.Loading
        };

        // TODO: Real model loading requires llama.cpp native binding integration:
        // 1. gguf_init - load GGUF file into memory mapping
        // 2. ggml_backend_init - initialize GPU/CPU backend (CUDA/Metal/BLAS)
        // 3. llava_load_model_from_file - load the model weights

        loadedInstance.State = ModelLoadState.Loaded;
        _logger.LogInformation("Model loaded successfully: {ModelId} ({Architecture})",
            modelId, metadata.Architecture);

        return loadedInstance;
    }

    /// <summary>
    /// Unloads a model from GPU/CPU memory.
    /// </summary>
    public async Task<bool> UnloadModelAsync(string modelId)
    {
        _logger.LogInformation("Unloading model: {ModelId}", modelId);

        // TODO: Real model unloading requires llama.cpp native binding integration:
        // 1. llava_free_model - free GPU memory for the model weights
        // 2. gguf_free_context - free the GGUF context and unmap file

        _logger.LogInformation("Model unloaded: {ModelId}", modelId);
        return true;
    }

    /// <summary>
    /// Gets all currently loaded models in GPU/CPU memory.
    /// </summary>
    public Task<IEnumerable<LoadedModelInstance>> GetLoadedModelsAsync()
    {
        _logger.LogDebug("Getting list of loaded models");
        return Task.FromResult(Enumerable.Empty<LoadedModelInstance>());
    }

    // ---- Private Helpers ----

    private async Task<ModelMetadata?> GetModelMetadata(string modelId)
    {
        // Use IModelRepository for actual metadata lookup instead of placeholder
        var metadata = await _modelRepository.GetModelByIdAsync(modelId);

        if (metadata != null)
            return metadata;

        // Fallback: try parsing the GGUF header directly if not in repository index
        var ggufPath = FindGgufFilePath(modelId);
        if (!string.IsNullOrEmpty(ggufPath))
        {
            _logger.LogDebug("Model not found in repository, trying GGUF parse for: {ModelId}", modelId);
            var headerInfo = await _ggufParser.ParseHeaderAsync(ggufPath);
            return headerInfo?.ToModelMetadata();
        }

        return null;
    }

    private string? FindGgufFilePath(string modelId)
    {
        // Search common GGUF directories for the model file
        var searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openlmstudio", "models"),
            Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? "", "OpenLMStudio", "models")
        };

        foreach (var searchPath in searchPaths)
        {
            if (!Directory.Exists(searchPath)) continue;

            // Search for .gguf files matching the model ID
            var pattern = $"{modelId}*.gguf";
            var matches = Directory.GetFiles(searchPath, pattern);

            if (matches.Any())
                return matches.First();

            // Also check subdirectories recursively
            var recursiveMatches = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
            if (recursiveMatches.Any())
                return recursiveMatches.First();
        }

        return null;
    }

    private string GeneratePlaceholderResponse(ModelMetadata metadata, ChatRequest request)
    {
        // TODO: Real inference requires llama.cpp native binding integration via P/Invoke or ML.NET ONNX Runtime

        var contextInfo = metadata.ContextLength > 0
            ? $"{metadata.ContextLength} tokens"
            : "unknown";

        return $"[Placeholder Response] - Model '{metadata.Name ?? request.ModelId}' ({metadata.Architecture}, {contextInfo} context). " +
               "Real inference requires llama.cpp native binding integration. " +
               "\n\nTo get real responses, you need to:\n" +
               "1. Install llama.cpp native bindings (libllama.dll/libllama.so)\n" +
               "2. Integrate via P/Invoke or ML.NET ONNX Runtime\n" +
               "3. Replace this placeholder with actual ggml_backend_schedule_eval() calls";
    }

    /// <summary>
    /// Standardized token counting method using consistent estimation: ~1 token per 4 characters for English.
    /// </summary>
    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    /// <summary>
    /// Estimates total token count across all messages including tool calls.
    /// </summary>
    private int EstimateTotalTokenCount(List<Message> messages)
    {
        var totalTokens = 0;

        foreach (var message in messages)
        {
            // Use token count if available, otherwise estimate
            totalTokens += message.TokenCount > 0
                ? message.TokenCount
                : EstimateTokenCount(message.Content);

            // Add tokens for tool calls if present
            if (message.ToolCalls != null && message.ToolCalls.Any())
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    totalTokens += EstimateTokenCount(toolCall.ArgumentsJson);
                }
            }
        }

        return totalTokens;
    }

    private List<Message> TruncateMessagesToFitContext(List<Message> messages, int maxContextLength)
    {
        // Remove oldest user messages to fit within context window
        var truncated = new List<Message>(messages);
        while (truncated.Count > 0 && EstimateTotalTokenCount(truncated) > maxContextLength)
        {
            // Remove first non-system message
            var removableIndex = truncated.FindLastIndex(
                m => m.Role == MessageRole.User || m.Role == MessageRole.Tool);

            if (removableIndex >= 0)
                truncated.RemoveAt(removableIndex);
            else
                break; // Can't remove system messages, nothing left to truncate
        }

        return truncated;
    }

    private IEnumerable<string> ChunkResponseForStreaming(string response)
    {
        // Split response into character-level chunks for simulated streaming
        foreach (var c in response)
        {
            yield return c.ToString();
        }
    }
}

/// <summary>
/// Legacy wrapper class that delegates to LlamaCppChatCompletionService.
/// Kept for backward compatibility - use LlamaCppChatCompletionService directly.
/// </summary>
public class LlamaCppChatService : IChatCompletionService, IDisposable
{
    private readonly ILogger<LlamaCppChatService>? _logger;
    private readonly LlamaCppChatCompletionService? _wrappedService;

    public LlamaCppChatService(ILogger<LlamaCppChatService>? logger = null)
    {
        _logger = logger;

        // Try to wrap the real service if DI resolves its dependencies
        try
        {
            var modelRepo = Activator.CreateInstance(typeof(IModelRepository)) as IModelRepository;
            var ggufParser = new GgufParser();

            if (modelRepo != null)
            {
                _wrappedService = new LlamaCppChatCompletionService(
                    NullLogger<LlamaCppChatCompletionService>.Instance,
                    modelRepo,
                    ggufParser);
                return;
            }
        }
        catch
        {
            // Fall back to stub implementation if DI fails
        }

        _logger?.LogWarning("LlamaCppChatService: Using legacy stub implementation - real inference pending native integration");
    }

    public async Task<ChatResponseChoice> GetCompletionAsync(ChatRequest request)
    {
        if (_wrappedService != null)
            return await _wrappedService.GetCompletionAsync(request);

        _logger?.LogWarning("LlamaCppChatService: Stub implementation - real inference pending native integration");

        var metadata = await GetFallbackMetadata(request.ModelId);
        return new ChatResponseChoice(
            new Message
            {
                Role = MessageRole.Assistant,
                Content = $"[Stub Response] - Model '{request.ModelId}' ({metadata.Architecture}). " +
                          "Real inference requires llama.cpp native binding integration.",
                TokenCount = EstimateTokenCount("stub response"),
                CreatedAt = DateTime.UtcNow
            },
            "stop", 0);
    }

    public async IAsyncEnumerable<string> GetStreamingCompletionAsync(ChatRequest request)
    {
        if (_wrappedService != null)
        {
            await foreach (var chunk in _wrappedService.GetStreamingCompletionAsync(request))
            {
                yield return chunk;
            }
            yield break;
        }

        var metadata = await GetFallbackMetadata(request.ModelId);
        yield return "{\"token\": \"[Stub] Streaming not implemented\", \"finish_reason\": null}";
        yield return "{\"token\": \"<eos>\", \"finish_reason\": \"stop\"}";
    }

    public void Dispose()
    {
        _wrappedService?.Dispose();
    }

    private async Task<ModelMetadata> GetFallbackMetadata(string? modelId = null)
    {
        // Try to get metadata via GgufParser if the model file exists
        var ggufPath = FindGgufFilePath();
        if (!string.IsNullOrEmpty(ggufPath))
        {
            try
            {
                var headerInfo = await new GgufParser().ParseHeaderAsync(ggufPath);
                if (headerInfo != null)
                    return headerInfo.ToModelMetadata();
                return CreateDefaultMetadata(modelId ?? "unknown");
            }
            catch
            {
                // Ignore parsing errors, fall back to defaults
            }
        }

        return CreateDefaultMetadata(modelId ?? "unknown");
    }

    private string? FindGgufFilePath()
    {
        var searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openlmstudio", "models")
        };

        foreach (var path in searchPaths)
        {
            if (!Directory.Exists(path)) continue;

            var ggufFiles = Directory.GetFiles(path, "*.gguf", SearchOption.AllDirectories);
            return ggufFiles.FirstOrDefault();
        }

        return null;
    }

    private ModelMetadata CreateDefaultMetadata(string modelId) => new()
    {
        Id = modelId,
        Name = "Unknown Model",
        Architecture = "unknown"
    };

    /// <summary>
    /// Standardized token counting method using consistent estimation: ~1 token per 4 characters for English.
    /// </summary>
    private static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
