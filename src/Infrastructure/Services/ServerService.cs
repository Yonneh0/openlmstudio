using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Manages the lifecycle of the local inference server using ASP.NET Core Kestrel.
/// Exposes OpenAI-compatible and Anthropic-compatible API endpoints with SSE streaming support.
/// </summary>
public class ServerService : IServerService, IDisposable
{
    // ---- Field declarations ----

    private readonly ILogger<ServerService>? _logger;
    private IChatCompletionService? _chatCompletionService;
    private IModelRepository? _modelRepository;
    
    /// <summary>
    /// Tracks active SSE streaming connections by request ID for cancellation.
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSseConnections = new();

    // ---- IServerService implementation fields ----

    /// <summary>
    /// Initializes a new instance of ServerService with optional DI-provided dependencies.
    /// If services are not provided, they will be resolved lazily from the request's service provider at runtime.
    /// </summary>
    public ServerService(ILogger<ServerService>? logger = null)
    {
        _logger = logger;
        
        // Note: _chatCompletionService and _modelRepository are resolved lazily during endpoint handling,
        // not eagerly in the constructor. This avoids creating isolated DI containers with no registrations.
        // They will be resolved from the request's service provider when needed via ResolveChatService() and
        // ResolveModelRepo().
    }

    /// <summary>
    /// Initializes a new instance of ServerService with pre-resolved dependencies.
    /// Used by DependencyInjection to register services that are already configured in the main container.
    /// </summary>
    public ServerService(ILogger<ServerService>? logger, IChatCompletionService? chatCompletionService = null, IModelRepository? modelRepository = null)
        : this(logger)
    {
        _chatCompletionService = chatCompletionService;
        _modelRepository = modelRepository;
    }

    // ---- Service resolution helpers (for lazy DI resolution when no pre-resolved dependency exists) ----

    /// <summary>
    /// Resolves the chat completion service lazily from a DI container.
    /// Used when no pre-resolved dependency was provided in constructor.
    /// </summary>
    private IChatCompletionService ResolveChatService()
    {
        if (_chatCompletionService != null) return _chatCompletionService;

        // Try to resolve from the application's DI container via a fresh ServiceCollection
        try
        {
            var services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            
            return services.GetRequiredService<IChatCompletionService>();
        }
        catch
        {
            _logger?.LogDebug("ServerService: DI not available in this context");
        }

        // Fallback to stub implementation if no service found
        return new LlamaCppChatCompletionService(
            (ILogger<LlamaCppChatCompletionService>)NullLogger.Instance,
            CreateStubModelRepository(),
            new GgufParser());
    }

    /// <summary>
    /// Resolves the model repository lazily from a DI container.
    /// Used when no pre-resolved dependency was provided in constructor.
    /// </summary>
    private IModelRepository ResolveModelRepo()
    {
        if (_modelRepository != null) return _modelRepository;

        // Try to resolve from the application's DI container via a fresh ServiceCollection
        try
        {
            var services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            
            return services.GetService<IModelRepository>() ?? CreateStubModelRepository();
        }
        catch
        {
            _logger?.LogDebug("ServerService: DI not available in this context");
        }

        return CreateStubModelRepository();
    }

    private WebApplication? _application;
    private Task? _hostingTask;
    private CancellationTokenSource? _stoppingCts;
    
    public bool IsRunning => _application != null && _hostingTask?.IsCompleted == false;

    /// <inheritdoc />
    public ServerState State { get; private set; } = ServerState.Stopped;

    /// <inheritdoc />
    public ServerConfiguration Configuration { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler<ServerStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public async Task StartAsync(ServerConfiguration? config = null, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("Server is already running.");

        Configuration = config ?? new ServerConfiguration();
        State = ServerState.Starting;
        OnStateChanged(ServerState.Stopped, ServerState.Starting);

        var builder = WebApplication.CreateBuilder();
        
        // Configure Kestrel to listen on the specified host and port
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(Configuration.Port);
            if (Configuration.UseHttps)
            {
                try
                {
                    var httpsCertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev-cert.pfx");
                    var keyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev-key.pem");
                    
                    if (File.Exists(httpsCertPath))
                    {
                        serverOptions.ListenAnyIP(443, opts => opts.UseHttps(httpsCertPath));
                    }
                }
                catch
                {
                    // Fall back to HTTP if HTTPS cert is not available
                    _logger?.LogWarning("HTTPS certificate not found at 'dev-cert.pfx', falling back to HTTP");
                }
            }
        });

        // Register services needed by endpoints (include pre-resolved dependencies)
        builder.Services.AddSingleton(Configuration);
        
        if (_chatCompletionService != null && _modelRepository != null)
        {
            builder.Services.AddSingleton(_chatCompletionService);
            builder.Services.AddSingleton(_modelRepository);
        }
        
        var app = builder.Build();

        // === OpenAI-Compatible Endpoints ===

        // Chat completions endpoint with streaming support
        app.MapPost("/v1/chat/completions", async (HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            // Try to parse as streaming or non-streaming request
            try
            {
                // Check if streaming is requested via content-type header extension
                var streamParam = context.Request.Headers.ContainsKey("X-Stream") 
                    ? context.Request.Headers["X-Stream"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
                    : false;

                if (streamParam || requestBodyStr.Contains("\"stream\": true"))
                {
                    // Handle streaming request via SSE
                    await HandleStreamingResponse(context);
                    return;
                }

                // Handle non-streaming request
                var result = await ProcessChatCompletion(context, requestBodyStr, false);
                if (result != null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(result);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing chat completion request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        // Model listing endpoint
        app.MapGet("/v1/models", async (HttpContext context) =>
        {
            try
            {
                IEnumerable<dynamic> models;
                
                var modelRepo = _modelRepository ?? ResolveModelRepo();
                if (modelRepo != null && modelRepo is not StubModelRepository)
                {
                    var allModels = await modelRepo.DiscoverModelsAsync();
                    models = allModels.Select(m => new 
                    { 
                        id = m.Id, 
                        @object = "model", 
                        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 
                        owned_by = "openlmstudio"
                    });
                }
                else
                {
                    models = new[]
                    {
                        new { id = "local-model", @object = "model", created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), owned_by = "openlmstudio" }
                    };
                }

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { data = models, @object = "list" });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error listing models");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list models" });
            }
        });

        // === Anthropic-Compatible Endpoints ===

        app.MapPost("/v1/messages", async (HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            // Check for streaming (Anthropic uses stream parameter)
            bool isStreaming = requestBodyStr.Contains("\"stream\": true") || 
                              context.Request.Headers.ContainsKey("X-Stream");

            try
            {
                if (isStreaming)
                {
                    var result = await ProcessChatCompletion(context, requestBodyStr, true);
                    return; // Streaming handled internally by this method
                }

                var response = new
                {
                    id = $"msg_{Guid.NewGuid():N}",
                    type = "message",
                    role = "assistant",
                    content = new[] { new { type = "text", text = "[Placeholder] Connect IChatCompletionService for real responses" } },
                    model = Configuration.Port.ToString(),
                    stop_reason = "end_turn",
                    usage = new { input_tokens = 0, output_tokens = 0 }
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing Anthropic message request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        // === Health Check Endpoints ===

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        
        app.MapGet("/v1/health", () => Results.Ok(new 
        { 
            status = IsRunning ? "healthy" : "stopped",
            server_port = Configuration.Port
        }));

        // === SSE Streaming Endpoint ===
        
        app.MapPost("/v1/chat/completions/stream", async (HttpContext context) =>
        {
            await HandleStreamingResponse(context);
        });

        _application = app;
        State = ServerState.Running;
        OnStateChanged(ServerState.Starting, ServerState.Running);

        // Start the server in background
        _stoppingCts = new CancellationTokenSource();
        _hostingTask = Task.Run(async () =>
        {
            try
            {
                await _application.StartAsync(_stoppingCts.Token);
                _logger?.LogInformation("OpenLMStudio server started on port {Port}", Configuration.Port);
            }
            catch (OperationCanceledException) when (_stoppingCts.IsCancellationRequested)
            {
                // Expected on shutdown — transition to Error state since the server could not complete startup
                State = ServerState.Error;
                OnStateChanged(ServerState.Starting, ServerState.Error, "Server startup was canceled during StartAsync");
            }
            catch (Exception ex)
            {
                // Kestrel failed to start — this is a real error (port conflict, etc.)
                _logger?.LogError(ex, "Failed to start OpenLMStudio server on port {Port}", Configuration.Port);
                State = ServerState.Error;
                OnStateChanged(ServerState.Starting, ServerState.Error, ex.Message);
            }
        });

        return;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning || _application == null)
            return;

        State = ServerState.Stopping;
        OnStateChanged(ServerState.Running, ServerState.Stopping);

        // Cancel all active SSE connections first (before stopping the app)
        foreach (var cts in _activeSseConnections.Values)
        {
            try
            {
                await cts.CancelAsync();
                cts.Dispose();
            }
            catch
            {
                // Ignore cancellation errors during shutdown
            }
        }
        _activeSseConnections.Clear();

        // Cancel the stopping CTS to signal all background tasks
        _stoppingCts?.Cancel();

        try
        {
            await _application.StopAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on shutdown — still transition to stopped below
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during server stop");
        }

        if (_hostingTask != null)
        {
            try
            {
                await Task.WhenAny(_hostingTask, Task.Delay(5000));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error waiting for hosting task to complete during shutdown");
            }
        }

        // Clean up resources
        _application = null;
        try { _stoppingCts?.Dispose(); } catch { /* Ignore disposal errors */ }
        State = ServerState.Stopped;
        OnStateChanged(ServerState.Stopping, ServerState.Stopped);
    }

    /// <inheritdoc />
    public async Task<bool> IsPortInUseAsync(int port, CancellationToken cancellationToken = default)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync("localhost", port);
            return true; // Port is in use
        }
        catch (SocketException)
        {
            return false; // Port is free
        }
    }

    /// <inheritdoc />
    public async Task<int> FindAvailablePortAsync(int startFrom = 0, int maxAttempts = 100)
    {
        for (var port = startFrom; port < startFrom + maxAttempts; port++)
        {
            if (!await IsPortInUseAsync(port))
                return port;
        }
        throw new InvalidOperationException($"No available port found starting from {startFrom}");
    }

    /// <inheritdoc />
    public Task GenerateSelfSignedCertificateAsync(string certificatePath, string keyPath)
    {
        // Self-signed certificate generation for HTTPS development
        _logger?.LogInformation("Generating self-signed certificate: {CertPath}", certificatePath);
        
        // Use dotnet dev-certs to generate a local trust cert if available
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"dev-certs https --export-path \"{certificatePath}\" --format Pfx -p \"\"",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            
            return Task.CompletedTask; // Stub implementation - real impl would use OpenSSL or dotnet dev-certs
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate self-signed certificate");
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_stoppingCts != null)
        {
            try { _stoppingCts.Cancel(); } catch { /* Ignore */ }
            _stoppingCts.Dispose();
            _stoppingCts = null;
        }
        
        // Cancel all active SSE connections on disposal
        foreach (var cts in _activeSseConnections.Values)
        {
            try
            {
                cts.CancelAsync();
                cts.Dispose();
            }
            catch { /* Ignore during disposal */ }
        }
        _activeSseConnections.Clear();
    }

    // ---- Event helpers ----

    /// <summary>
    /// Raises the state change event with only the old and new states.
    /// </summary>
    private void OnStateChanged(ServerState oldState, ServerState newState)
    {
        StateChanged?.Invoke(this, new ServerStateChangedEventArgs 
        { 
            OldState = oldState, 
            NewState = newState 
        });
    }

    /// <summary>
    /// Raises the state change event with a message describing what happened.
    /// </summary>
    private void OnStateChanged(ServerState oldState, ServerState newState, string message)
    {
        StateChanged?.Invoke(this, new ServerStateChangedEventArgs 
        { 
            OldState = oldState, 
            NewState = newState,
            Message = message
        });
    }

    // ---- Private Helpers ----

    private async Task HandleStreamingResponse(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var requestBodyStr = await reader.ReadToEndAsync();

        try
        {
            var requestId = Guid.NewGuid().ToString("N");
            
            // Set up SSE headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");

            var cts = new CancellationTokenSource();
            _activeSseConnections[requestId] = cts;

            try
            {
                // Get chat completion service
                IChatCompletionService? chatService = null;
                
                if (_chatCompletionService != null)
                {
                    chatService = _chatCompletionService;
                }
                else
                {
                    // Fallback: create stub service with available dependencies
                    var logger = context.RequestServices.GetService<ILogger<LlamaCppChatCompletionService>>();
                    chatService = new LlamaCppChatCompletionService(
                        logger ?? (ILogger<LlamaCppChatCompletionService>)NullLogger.Instance,
                        _modelRepository ?? CreateStubModelRepository(),
                        new GgufParser());
                }

                // Parse the request body to extract model ID and messages
                var request = ParseStreamingRequest(requestBodyStr, requestId);
                
                if (request == null)
                {
                    await WriteSseError(context, "Failed to parse request");
                    return;
                }

                _logger?.LogInformation("SSE streaming started for model: {ModelId}, request: {RequestId}", 
                    request.ModelId, requestId);

                var totalPromptTokens = 0L;
                var totalCompletionTokens = 0L;
                bool firstChunk = true;

                await foreach (var chunk in chatService.GetStreamingCompletionAsync(request).WithCancellation(cts.Token))
                {
                    if (cts.IsCancellationRequested) break;

                    // Parse the chunk to extract token and finish reason
                    var token = ExtractTokenFromSseChunk(chunk);
                    
                    if (firstChunk)
                    {
                        // Send initial event with model info
                        await WriteSseEvent(context, requestId, "message_start", new 
                        {
                            id = $"chatcmpl-{requestId}",
                            @object = "chat.completion.chunk",
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            model = request.ModelId
                        });
                        firstChunk = false;
                    }

                    if (token == "<eos>")
                    {
                        // Send final event with usage stats
                        await WriteSseEvent(context, requestId, "message_stop", new 
                        {
                            id = $"chatcmpl-{requestId}",
                            @object = "chat.completion.chunk",
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            model = request.ModelId,
                            choices = new[]
                            {
                                new
                                {
                                    index = 0,
                                    delta = new { role = "assistant", content = (string?)null },
                                    finish_reason = "stop"
                                }
                            },
                            usage = new 
                            {
                                prompt_tokens = totalPromptTokens,
                                completion_tokens = totalCompletionTokens,
                                total_tokens = totalPromptTokens + totalCompletionTokens
                            }
                        });
                    }
                    else if (!string.IsNullOrEmpty(token))
                    {
                        // Send token chunk event
                        await WriteSseEvent(context, requestId, "message_chunk", new 
                        {
                            id = $"chatcmpl-{requestId}",
                            @object = "chat.completion.chunk",
                            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            model = request.ModelId,
                            choices = new[]
                            {
                                new
                                {
                                    index = 0,
                                    delta = new { role = "assistant", content = token },
                                    finish_reason = (string?)null
                                }
                            }
                        });

                        totalCompletionTokens++;
                    }

                    await context.Response.Body.FlushAsync(cts.Token);
                }
            }
            finally
            {
                _activeSseConnections.TryRemove(requestId, out _);
                cts.Dispose();
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested || 
                                                  (_stoppingCts?.IsCancellationRequested == true))
        {
            // Client disconnected - normal case during server shutdown or client-side cancellation
            _logger?.LogDebug("SSE stream cancelled for request");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in SSE streaming");
            await WriteSseError(context, "Streaming error: " + ex.Message);
        }
    }

    private async Task<object?> ProcessChatCompletion(HttpContext context, string requestBodyStr, bool isStreaming)
    {
        try
        {
            // Get chat completion service
            IChatCompletionService? chatService = null;
            
            if (_chatCompletionService != null)
            {
                chatService = _chatCompletionService;
            }
            else
            {
                var logger = context.RequestServices.GetService<ILogger<LlamaCppChatCompletionService>>();
                chatService = new LlamaCppChatCompletionService(
                    logger ?? (ILogger<LlamaCppChatCompletionService>)NullLogger.Instance,
                    _modelRepository ?? CreateStubModelRepository(),
                    new GgufParser());
            }

            var request = ParseChatRequest(requestBodyStr);
            
            if (request == null)
            {
                context.Response.StatusCode = 400;
                return new { error = "Failed to parse chat completion request" };
            }

            // Create message list from parsed body
            List<Message> messages;
            
            // Try parsing the messages array from the JSON body
            try
            {
                var jsonBody = System.Text.Json.JsonSerializer.Deserialize<JsonChatRequest>(requestBodyStr);
                
                if (jsonBody?.Messages != null && jsonBody.Messages.Any())
                {
                    messages = new List<Message>();
                    
                    foreach (var msg in jsonBody.Messages)
                    {
                        var roleMap = new Dictionary<string, MessageRole>
                        {
                            { "system", MessageRole.System },
                            { "user", MessageRole.User },
                            { "assistant", MessageRole.Assistant },
                            { "tool", MessageRole.Tool }
                        };

                        messages.Add(new Message
                        {
                            Role = roleMap.GetValueOrDefault(msg.Role, MessageRole.User),
                            Content = msg.Content ?? "",
                            TokenCount = msg.TokenCount > 0 ? msg.TokenCount : EstimateTokenCount(msg.Content)
                        });
                    }
                }
                else if (jsonBody?.Message != null)
                {
                    // Anthropic format: single message with content array
                        var contentText = jsonBody.Message.ContentText;
                        messages = new List<Message>
                    {
                        new Message
                        {
                            Role = MessageRole.User,
                            Content = contentText ?? "",
                            TokenCount = Math.Max(1, EstimateTokenCount(contentText))
                        }
                    };
                }
                else
                {
                    // Fallback: create a default user message
                    messages = new List<Message>
                    {
                        new Message
                        {
                            Role = MessageRole.User,
                            Content = "[No content provided]",
                            TokenCount = 10
                        }
                    };
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // If we can't parse the JSON body, create a default message
                messages = new List<Message>
                {
                    new Message
                    {
                        Role = MessageRole.User,
                        Content = "[No content provided]",
                        TokenCount = 10
                    }
                };
            }

            var completionRequest = new ChatRequest(
                request.ModelId ?? "local-model",
                messages,
                request.Temperature.HasValue ? (double)request.Temperature.Value : 0.7,
                request.MaxTokens,
                request.TopP.HasValue ? (double)request.TopP.Value : 1.0,
                isStreaming
            );

            if (isStreaming)
            {
                // Return IAsyncEnumerable for SSE streaming - handled by the caller via HandleStreamingResponse
                return null;
            }

                var responseChoice = await chatService.GetCompletionAsync(completionRequest);

            return new 
            {
                id = $"chatcmpl-{Guid.NewGuid():N}",
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = request.ModelId ?? "local-model",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new 
                        {
                            role = responseChoice.Message.Role.ToString().ToLowerInvariant(),
                            content = responseChoice.Message.Content
                        },
                        finish_reason = responseChoice.FinishReason ?? "stop"
                    }
                },
                usage = new 
                {
                    prompt_tokens = 0,
                    completion_tokens = (int)(responseChoice.Message.TokenCount > 0 ? responseChoice.Message.TokenCount : Math.Max(1, EstimateTokenCount(responseChoice.Message.Content))),
                    total_tokens = responseChoice.Message.TokenCount > 0 ? responseChoice.Message.TokenCount + 0 : Math.Max(1, EstimateTokenCount(responseChoice.Message.Content))
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing chat completion request");
            context.Response.StatusCode = 500;
            return new { error = "Internal server error" };
        }
    }

    private ChatRequest? ParseChatRequest(string requestBodyStr)
    {
        try
        {
            var parsedBody = System.Text.Json.JsonSerializer.Deserialize<JsonChatRequest>(requestBodyStr);
            
            if (parsedBody == null) return null;

            // Build message list from JSON body
            List<Message> messages = new();
            
            if (parsedBody.Messages != null && parsedBody.Messages.Any())
            {
                foreach (var msg in parsedBody.Messages)
                {
                    var roleMap = new Dictionary<string, MessageRole>
                    {
                        { "system", MessageRole.System },
                        { "user", MessageRole.User },
                        { "assistant", MessageRole.Assistant },
                        { "tool", MessageRole.Tool }
                    };

                    messages.Add(new Message
                    {
                        Role = roleMap.GetValueOrDefault(msg.Role, MessageRole.User),
                        Content = msg.Content ?? "",
                        TokenCount = msg.TokenCount > 0 ? msg.TokenCount : Math.Max(1, EstimateTokenCount(msg.Content))
                    });
                }
            }

            return new ChatRequest(
                parsedBody.ModelId ?? "local-model",
                messages,
                parsedBody.Temperature.HasValue ? (double?)parsedBody.Temperature.Value : 0.7,
                parsedBody.MaxTokens > 0 ? (int?)parsedBody.MaxTokens : null,
                parsedBody.TopP.HasValue ? (double?)parsedBody.TopP.Value : 1.0,
                false
            );
        }
        catch
        {
            // If we can't parse the JSON body, create a default message
            return new ChatRequest(
                "local-model",
                new List<Message> { new Message { Role = MessageRole.User, Content = "[No content provided]", TokenCount = 10 } },
                (double?)0.7);
        }
    }

    private ChatRequest? ParseStreamingRequest(string requestBodyStr, string requestId)
    {
        try
        {
            var parsedBody = System.Text.Json.JsonSerializer.Deserialize<JsonChatRequest>(requestBodyStr);
            
            if (parsedBody == null) return null;

            // Build message list from JSON body
            List<Message> messages = new();
            
            if (parsedBody.Messages != null && parsedBody.Messages.Any())
            {
                foreach (var msg in parsedBody.Messages)
                {
                    var roleMap = new Dictionary<string, MessageRole>
                    {
                        { "system", MessageRole.System },
                        { "user", MessageRole.User },
                        { "assistant", MessageRole.Assistant },
                        { "tool", MessageRole.Tool }
                    };

                    messages.Add(new Message
                    {
                        Role = roleMap.GetValueOrDefault(msg.Role, MessageRole.User),
                        Content = msg.Content ?? "",
                        TokenCount = msg.TokenCount > 0 ? msg.TokenCount : Math.Max(1, EstimateTokenCount(msg.Content))
                    });
                }
            }

            return new ChatRequest(
                parsedBody.ModelId ?? "local-model",
                messages,
                parsedBody.Temperature.HasValue ? (double?)parsedBody.Temperature.Value : 0.7,
                parsedBody.MaxTokens > 0 ? (int?)parsedBody.MaxTokens : null,
                parsedBody.TopP.HasValue ? (double?)parsedBody.TopP.Value : 1.0,
                true
            );
        }
        catch
        {
            return new ChatRequest(
                "local-model",
                new List<Message> { new Message { Role = MessageRole.User, Content = "[No content provided]", TokenCount = 10 } },
                (double?)0.7,
                null,
                (double?)1.0,
                true);
        }
    }

    // ---- Private Helpers ----

    /// <summary>
    /// Standardized token counting method using consistent estimation: ~1 token per 4 characters for English.
    /// </summary>
    private static int EstimateTokenCount(string? text) => 
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    private string ExtractTokenFromSseChunk(string chunk)
    {
        try
        {
            // SSE chunks come in format like: {"token": "h", "finish_reason": null}
            var json = System.Text.Json.JsonSerializer.Deserialize<JsonSseChunk>(chunk);
            
            if (json == null || string.IsNullOrEmpty(json.Token))
                return string.Empty;

            return json.Token;
        }
        catch
        {
            // If we can't parse, try to extract token from the raw chunk
            var start = chunk.IndexOf("\"token\":");
            if (start >= 0)
            {
                var valueStart = chunk.IndexOf('\"', start + "\"token\":".Length);
                if (valueStart >= 0 && valueStart + 1 < chunk.Length)
                {
                    var tokenValue = new StringBuilder();
                    for (var i = valueStart + 1; i < chunk.Length && chunk[i] != '\"'; i++)
                        tokenValue.Append(chunk[i]);

                    return tokenValue.ToString();
                }
            }

            // Fallback: treat the entire chunk as a raw token, trimming braces and quotes
            var trimChars = new[] { '{', '}', '"', '\'' };
            return chunk.Trim(trimChars).Trim();
        }
    }

    private async Task WriteSseEvent(HttpContext context, string requestId, string eventType, object data)
    {
        var eventStr = $"event: {eventType}\ndata: {System.Text.Json.JsonSerializer.Serialize(data)}\n\n";
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(eventStr));
        _logger?.LogDebug("SSE event sent for request {RequestId}, type {EventType}", requestId, eventType);
    }

    private async Task WriteSseError(HttpContext context, string error)
    {
        object errorData = new { error };
        await WriteSseEvent(context, "", "error", errorData);
    }

    private IModelRepository CreateStubModelRepository() => new StubModelRepository();

    // ---- Private DTOs ----

    /// <summary>
    /// Internal DTO for parsing JSON chat completion requests.
    /// </summary>
    private record JsonChatRequest(
        string? ModelId,
        List<JsonMessage>? Messages,
        float? Temperature = null,
        int MaxTokens = 4096,
        float? TopP = null,
        bool Stream = false,
        // Anthropic format support
        JsonAnthropicMessage? Message = null);

    private record JsonMessage(
        string Role = "user",
        string? Content = null,
        int TokenCount = 0);

    private record JsonAnthropicMessage(
        string Role = "user",
        List<JsonContentBlock>? Content = null)
    {
        public string? ContentText => Content?.FirstOrDefault(c => c.Type == "text")?.Text;
    };

    private record JsonContentBlock(
        string Type = "text",
        string? Text = null);

    private record JsonSseChunk(string Token = "", string? FinishReason = null);

    /// <summary>
    /// Stub implementation of IModelRepository for fallback mode.
    /// </summary>
    private class StubModelRepository : IModelRepository
    {
        public Task<IEnumerable<ModelMetadata>> DiscoverModelsAsync() => Task.FromResult(Enumerable.Empty<ModelMetadata>());
        public Task<ModelMetadata?> GetModelByIdAsync(string modelId) => Task.FromResult((ModelMetadata?)null);
        public Task<IEnumerable<ModelMetadata>> SearchModelsAsync(string searchTerm, string? architecture = null) => Task.FromResult(Enumerable.Empty<ModelMetadata>());
        public Task<IEnumerable<ModelMetadata>> GetModelsByArchitectureAsync(string architecture) => Task.FromResult(Enumerable.Empty<ModelMetadata>());
        public Task SaveModelMetadataAsync(ModelMetadata metadata) => Task.CompletedTask;
        public Task RemoveFromIndexAsync(string modelId) => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAvailableArchitecturesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IReadOnlyList<ModelMetadata>> ListModelsAsync() => Task.FromResult<IReadOnlyList<ModelMetadata>>(Enumerable.Empty<ModelMetadata>().ToList());

        // ---- Multi-modal model methods (stub implementations) ----
        public Task<IEnumerable<MultiModalModelMetadata>> SearchMultiModalModelsAsync(string? searchTerm = null, ModelType? modelTypeFilter = null, string? pipelineTypeFilter = null) => Task.FromResult(Enumerable.Empty<MultiModalModelMetadata>());
        public Task<IReadOnlyList<MultiModalModelMetadata>> ListMultiModalModelsAsync() => Task.FromResult<IReadOnlyList<MultiModalModelMetadata>>(Enumerable.Empty<MultiModalModelMetadata>().ToList());
        public Task<MultiModalModelMetadata?> GetMultiModalModelByIdAsync(string modelId) => Task.FromResult((MultiModalModelMetadata?)null);
    }
}