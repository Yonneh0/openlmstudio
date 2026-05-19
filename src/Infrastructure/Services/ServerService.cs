using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using System.Collections.Concurrent;
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
    /// Protected by _sseCleanupLock to prevent race conditions during concurrent cleanup.
    /// </summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSseConnections = new();

    /// <summary>
    /// Lock object for synchronizing SSE connection cleanup across multiple threads.
    /// </summary>
    private readonly object _sseCleanupLock = new();

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

    // ---- Service resolution helpers (resolve from HttpContext.RequestServices) ----

    /// <summary>
    /// Resolves the chat completion service from an HttpContext's service provider.
    /// </summary>
    private IChatCompletionService ResolveChatService(HttpContext context)
    {
        if (_chatCompletionService != null) return _chatCompletionService;
        return context.RequestServices.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Resolves the model repository from an HttpContext's service provider.
    /// </summary>
    private IModelRepository? ResolveModelRepo(HttpContext context)
    {
        if (_modelRepository != null) return _modelRepository;
        return context.RequestServices.GetService<IModelRepository>();
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
                var httpsCertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev-cert.pfx");
                var keyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dev-key.pem");

                // If HTTPS is enabled but no cert exists yet, try to auto-generate one
                if (!File.Exists(httpsCertPath))
                {
                    _logger?.LogInformation("HTTPS certificate not found at '{CertPath}', attempting to generate...", httpsCertPath);
                    TryGenerateCertificate(httpsCertPath, keyPath).GetAwaiter().GetResult();

                    // Check again after generation attempt
                    if (!File.Exists(httpsCertPath))
                    {
                        _logger?.LogWarning("HTTPS certificate not found at '{CertPath}', falling back to HTTP", httpsCertPath);
                    }
                }

                var certToUse = File.Exists(httpsCertPath) ? httpsCertPath : keyPath; // Use whichever exists (PFX or PEM+key)

                try
                {
                    if (File.Exists(certToUse))
                    {
                        _logger?.LogInformation("HTTPS certificate found at '{CertPath}'", certToUse);

                        if (certToUse.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                        {
                            // Use PFX with password
                            serverOptions.ListenAnyIP(443, opts => opts.UseHttps(certToUse));
                        }
                        else if (File.Exists(keyPath))
                        {
                            // Use PEM cert + key pair for Kestrel
                            serverOptions.ListenAnyIP(443, opts => opts.UseHttps(certToUse, keyPath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to use HTTPS certificate at '{CertPath}', falling back to HTTP", certToUse);
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

        // Register rate limiter if enabled
        if (Configuration.EnableRateLimiting)
        {
            builder.Services.AddRateLimiting(Configuration.MaxRequestsPerMinute, TimeSpan.FromMinutes(1));
            _logger?.LogInformation("Rate limiting enabled: {MaxRequests} requests per minute", Configuration.MaxRequestsPerMinute);
        }

        // Register SSE reconnect tracking service always (needed for streaming reconnection)
        builder.Services.AddSseReconnectTracking();

        // Register CORS if configured
        if (Configuration.AllowCors)
        {
            var corsOrigins = Configuration.AllowedOrigins;
            if (!corsOrigins.Any())
            {
                // Default: allow all origins in development mode
                corsOrigins = new List<string> { "*" };
            }

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("OpenLMStudio", policy =>
                {
                    if (corsOrigins.Contains("*"))
                    {
                        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    }
                    else
                    {
                        policy.WithOrigins(corsOrigins.ToArray())
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    }
                });
            });

            _logger?.LogInformation("CORS enabled for origins: {Origins}", string.Join(", ", corsOrigins));
        }

        var app = builder.Build();

        // Apply CORS middleware if configured (must be before endpoints)
        if (Configuration.AllowCors)
        {
            app.UseCors("OpenLMStudio");
        }

        // Apply rate limiting if enabled (before endpoints, after CORS)
        if (Configuration.EnableRateLimiting)
        {
            app.UseMiddleware<RateLimitMiddleware>(Configuration.MaxRequestsPerMinute, TimeSpan.FromMinutes(1));
        }

        // Apply API key authentication middleware when configured (before endpoints, after rate limiting)
        if (!string.IsNullOrEmpty(Configuration.ApiKey))
        {
            app.UseApiKeyAuthentication();
        }

        // Register all endpoint groups — each group is a separate extension method for clarity and testability.
        app.UseChatCompletionEndpoints(Configuration, _logger);
        app.UseAnthropicEndpoints(Configuration, _logger);
        app.UseModelListEndpoints(Configuration, _logger);
        app.UseImageEndpoints(Configuration, _logger);
        app.UseHealthEndpoint();


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

        // Cancel all active SSE connections first (before stopping the app) — synchronize to prevent race conditions
        var connectionsToCancel = _activeSseConnections.Values.ToList();
        _activeSseConnections.Clear();
        lock (_sseCleanupLock)
        {
            foreach (var cts in connectionsToCancel)
            {
                try
                {
                    cts.CancelAsync().GetAwaiter().GetResult();
                    cts.Dispose();
                }
                catch
                {
                    // Ignore cancellation errors during shutdown
                }
            }
        }

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
    public async Task GenerateSelfSignedCertificateAsync(string certificatePath, string keyPath, CancellationToken ct = default)
    {
        // Self-signed certificate generation for HTTPS development using the new cert service
        _logger?.LogInformation("Generating self-signed certificate: {CertPath}", certificatePath);

        var certService = ResolveCertificateService();
        if (certService == null)
        {
            _logger?.LogError("Cannot generate certificate: ISelfSignedCertificateService not available");
            throw new InvalidOperationException("ISelfSignedCertificateService is required for HTTPS setup but was not found in DI.");
        }

        // Ensure directory exists before generating the cert
        var certDir = Path.GetDirectoryName(certificatePath);
        if (!string.IsNullOrEmpty(certDir) && !Directory.Exists(certDir))
        {
            Directory.CreateDirectory(certDir);
        }

        var success = await certService.GenerateCertificateAsync(certificatePath, keyPath, ct);

        if (success)
        {
            _logger?.LogInformation("Self-signed certificate generated successfully: {CertPath}", certificatePath);

            // Try to trust the certificate on Windows
            try
            {
                await certService.TrustCertificateAsync(certificatePath, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to auto-trust the generated certificate. Manual trust required.");
            }
        }
        else
        {
            throw new InvalidOperationException("Failed to generate self-signed certificate. Ensure OpenSSL is installed on this system.");
        }
    }

    /// <summary>
    /// Resolves the ISelfSignedCertificateService from DI container or returns null if not available.
    /// </summary>
    private ISelfSignedCertificateService? ResolveCertificateService()
    {
        // Try to resolve from any active application (if one exists)
        if (_application != null)
        {
            try
            {
                return _application.Services.GetService<ISelfSignedCertificateService>();
            }
            catch
            {
                // Ignore resolution errors during endpoint handling
            }
        }

        // Fallback: resolve from a fresh ServiceCollection with just logging and the cert service itself
        try
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<ISelfSignedCertificateService, SelfSignedCertificateGenerator>()
                .BuildServiceProvider();

            return services.GetRequiredService<ISelfSignedCertificateService>();
        }
        catch
        {
            _logger?.LogDebug("ServerService: Certificate service not available in this context");
        }

        return null;
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

        // Cancel all active SSE connections on disposal — synchronize to prevent race conditions
        var disposeConnections = _activeSseConnections.Values.ToList();
        _activeSseConnections.Clear();
        lock (_sseCleanupLock)
        {
            foreach (var cts in disposeConnections)
            {
                try
                {
                    cts.CancelAsync().GetAwaiter().GetResult();
                    cts.Dispose();
                }
                catch { /* Ignore during disposal */ }
            }
        }
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

    /// <summary>
    /// Attempts to auto-generate a self-signed HTTPS certificate if one doesn't exist.
    /// </summary>
    private static async Task TryGenerateCertificate(string httpsCertPath, string keyPath)
    {
        try
        {
        // Build command arguments using string interpolation for clarity
        var opensslArgs = $"req -x509 -newkey rsa:2048 -keyout \"{keyPath}\" -out \"{httpsCertPath}\" -days 365 -nodes -subj \"/CN=localhost\"";

            // Try various OpenSSL paths
            var possibleOpenSSLPaths = new[] {
                "openssl", // Check PATH first (Unix-like systems)
                @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
                @"C:\Program Files (x86)\OpenSSL-Win32\bin\openssl.exe"
            };

            foreach (var opensslPath in possibleOpenSSLPaths)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = opensslPath,
                        Arguments = string.Format(opensslArgs, httpsCertPath.Replace("\"", "\\\""), keyPath.Replace("\"", "\\\"")),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync();
                        if (File.Exists(httpsCertPath))
                        {
                            // Successfully generated — also copy the cert as .crt for wider compatibility
                            try
                            {
                                var crtPath = Path.ChangeExtension(httpsCertPath, ".crt");
                                File.Copy(httpsCertPath, crtPath, true);
                            }
                            catch { /* Ignore copy errors */ }
                            return; // Success — exit early
                        }
                    }
                }
                catch
                {
                    // OpenSSL at this path didn't work, try next one
                }
            }

            // All attempts failed — silently ignore since the user can manually generate a cert
        }
        catch
        {
            // Ignore any errors during auto-generation
        }
    }

    // ---- Private Helpers ----

    private async Task HandleStreamingResponse(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var requestBodyStr = await reader.ReadToEndAsync();

        try
        {
            var requestId = Guid.NewGuid().ToString("N");
            var connectionId = Guid.NewGuid().ToString("N")[..16]; // Shorter ID for SSE event tracking

            // Check if the client is reconnecting (Last-Event-ID header)
            string? reconnectFromEventId = null;
            if (context.Request.Headers.TryGetValue("Last-Event-ID", out var lastEventId))
            {
                reconnectFromEventId = lastEventId.ToString();
                _logger?.LogInformation("SSE reconnection detected from event ID: {EventId}", reconnectFromEventId);
            }

            // Set up SSE headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Event-ID", connectionId); // Send our event ID for future reconnects

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
                    // Resolve from HttpContext's service provider
                    chatService = ResolveChatService(context);
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
                // Resolve from HttpContext's service provider
                chatService = ResolveChatService(context);
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

            // Multi-engine routing: detect model type and route to correct engine
            var modelId = request.ModelId ?? "local-model";
            var modelRepo = ResolveModelRepo(context);
            if (modelRepo != null)
            {
                var modelType = await DetectModelTypeAsync(_logger, modelRepo, modelId);
                // If it's a multi-modal non-text model, route to appropriate endpoint
                if (modelType.HasValue && modelType.Value != ModelType.TextGeneration)
                {
                    context.Response.StatusCode = 400;
                    await WriteChatCompletionNotSupportedError(context, modelId);
                    return null;
                }
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
        catch (System.Text.Json.JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse chat request body — returning null to let caller handle the error");
            return null;
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

    /// <summary>
    /// Detects the model type from a model ID by looking it up in the repository.
    /// Returns null if the model cannot be found or is a GGUF text generation model.
    /// </summary>
    private static async Task<ModelType?> DetectModelTypeAsync(ILogger? logger, IModelRepository? repo, string modelId)
    {
        if (repo == null)
            return null;

        // Check multi-modal models first (image/diffusion/VAE/LoRA/embedding)
        var multimodal = await repo.GetMultiModalModelByIdAsync(modelId);
        if (multimodal != null)
            return multimodal.ModelType;

        // GGUF text generation model — explicitly NOT a multi-modal type
        logger?.LogDebug("Model '{ModelId}' resolved as GGUF text generation model", modelId);
        return ModelType.TextGeneration;
    }

    /// <summary>
    /// Writes an error response indicating that the requested model type is not supported for chat completion.
    /// </summary>
    private static async Task WriteChatCompletionNotSupportedError(HttpContext context, string modelId)
    {
        var suggestedEndpoint = "/v1/embeddings"; // Default suggestion

        // Determine which endpoint to suggest based on common naming conventions
        if (modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase))
            suggestedEndpoint = "/v1/embeddings";
        else if (modelId.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("diffusion", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("sdxl", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("flux", StringComparison.OrdinalIgnoreCase))
            suggestedEndpoint = "/v1/images/generations";

        await context.Response.WriteAsJsonAsync(new
        {
            error = "model_type_not_supported_for_chat_completion",
            message = $"Model '{modelId}' is not a text generation model and cannot be used with /v1/chat/completions.",
            suggested_endpoint = suggestedEndpoint,
            details = new[]
            {
                "Chat completion (text generation) models use GGUF format. Image generation, diffusion, VAE, LoRA, and embedding models require their respective endpoints."
            }
        });
    }

    /// <summary>
    /// Writes a 400 error response for missing or invalid model identifier.
    /// </summary>
    private static async Task WriteModelRequiredError(HttpContext context)
    {
        await context.Response.WriteAsJsonAsync(new
        {
            error = "missing_model_id",
            message = "A valid model identifier is required for this endpoint.",
            supported_endpoints = new[]
            {
                "/v1/chat/completions — text generation (GGUF format)",
                "/v1/images/generations — image generation (diffusion models)",
                "/v1/embeddings — embedding generation",
                "/v1/models/image/list — list available image generation models",
                "/v1/models/embedding/list — list available embedding models"
            }
        });
    }

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

    /// <summary>
    /// Handles streaming image generation via SSE — emits per-step progress updates during denoising.
    /// </summary>
    private async Task HandleImageGenerationStreaming(HttpContext context, IDiffusionPipelineService pipeline, ImageGenerationRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var connectionId = Guid.NewGuid().ToString("N")[..16];

        // Set up SSE headers for streaming
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Event-ID", connectionId);

        var cts = new CancellationTokenSource();
        _activeSseConnections[requestId] = cts;

        try
        {
            await WriteSseEvent(context, requestId, "generation_start", new
            {
                id = $"img_{requestId}",
                @object = "image.generation.chunk",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = request.ModelId,
                prompt = request.Prompt
            });

            // Emit progress updates during generation via StreamProgressAsync
            var progressTask = Task.Run(async () =>
            {
                await foreach (var progress in pipeline.StreamProgressAsync(request).WithCancellation(cts.Token))
                {
                    try
                    {
                        await WriteSseEvent(context, requestId, "progress", new
                        {
                            step = progress.Step,
                            total_steps = progress.TotalSteps,
                            percentage = Math.Round(progress.ProgressPercent, 1)
                        });
                        await context.Response.Body.FlushAsync(cts.Token);
                    }
                    catch
                    {
                        // Ignore cancellation during streaming
                    }
                }
            }, cts.Token);

            // Generate the image in background and stream final result when complete
            var imageTask = pipeline.GenerateImageAsync(request, cts.Token);

            await Task.WhenAll(progressTask, imageTask);

            if (imageTask.IsCompletedSuccessfully && imageTask.Result != null)
            {
                try
                {
                    // Cancel progress streaming now that we have the result
                    cts.Cancel();

                    var result = imageTask.Result;

                    // Send final event with image data
                    await WriteSseEvent(context, requestId, "generation_complete", new
                    {
                        id = $"img_{requestId}",
                        @object = "image.generation.chunk",
                        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        model = request.ModelId,
                        data = new[]
                        {
                            new
                            {
                                b64_json = Convert.ToBase64String(result.ImageBytes),
                                width = result.Width,
                                height = result.Height,
                                seed = result.Seed
                            }
                        }
                    });
                }
                catch
                {
                    // Ignore errors during final event streaming
                }
            }
            else if (imageTask.IsFaulted)
            {
                var ex = imageTask.Exception?.InnerException ?? new Exception("Image generation failed");
                await WriteSseError(context, $"Image generation failed: {ex.Message}");
            }
        }
        finally
        {
            _activeSseConnections.TryRemove(requestId, out _);
            cts.Dispose();
        }
    }

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

}

// ---- Anthropic API DTOs ----

/// <summary>
/// Internal DTO for parsing Anthropic-compatible message requests (for /v1/messages endpoint).
/// </summary>
internal record AnthropicRequest(
    string? Model = null,
    double? Temperature = 0.7,
    int MaxTokens = 4096,
    float? TopP = 1.0f,
    List<AnthropicMessage>? Messages = null,
    string? System = null);

/// <summary>
/// Internal DTO for parsing Anthropic-compatible message blocks (for /v1/messages endpoint).
/// </summary>
internal record AnthropicMessage(
    string Role = "user",
    List<ContentBlock>? ContentBlocks = null)
{
    /// <summary>Convenience accessor: returns the text content from all 'text' type content blocks.</summary>
    public string? Content => ContentBlocks != null && ContentBlocks.Any(cb => cb.Type == "text")
        ? string.Join("\n", ContentBlocks.Where(cb => cb.Type == "text").Select(cb => cb.Text!).Where(s => s != null)!)
        : null;
}

/// <summary>
/// Internal DTO for parsing Anthropic-compatible content blocks (for /v1/messages endpoint).
/// </summary>
internal record EmbeddingsRequest(
    string? Model = null,
    object Input = null!,
    string? EncodingFormat = "float",
    int? Dimensions = null);

/// <summary>
/// Internal DTO for parsing Anthropic-compatible content blocks (for /v1/messages endpoint).
/// </summary>
internal record ContentBlock(
    string Type = "text",
    string? Text = null);

// ---- Endpoint group extension methods ----

/// <summary>
/// Extension methods for grouping ServerService endpoints in ASP.NET Core.
/// </summary>
public static class ServerServiceEndpointExtensions
{
    /// <summary>
    /// Registers OpenAI-compatible chat completions endpoints (/v1/chat/completions, /v1/models).
    /// </summary>
    public static WebApplication UseChatCompletionEndpoints(
        this WebApplication app,
        ServerConfiguration config,
        ILogger? logger)
    {
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

            try
            {
                var streamParam = context.Request.Headers.ContainsKey("X-Stream")
                    ? context.Request.Headers["X-Stream"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
                    : false;

                if (streamParam || requestBodyStr.Contains("\"stream\": true"))
                {
                    // Streaming handled via embedded ServerService instance
                    await HandleStreamingResponse(context, config, logger);
                    return;
                }

                var result = await ProcessChatCompletion(context, requestBodyStr, false, config, logger);
                if (result != null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(result);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing chat completion request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        // Model listing endpoint
        app.MapGet("/v1/models", async (IModelRepository modelRepo, HttpContext context) =>
        {
            try
            {
                IEnumerable<dynamic> models;

                if (modelRepo != null)
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
                logger?.LogError(ex, "Error listing models");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list models" });
            }
        });

        return app;
    }

    /// <summary>
    /// Registers Anthropic-compatible endpoints (/v1/messages, /v1/models/embedding/list).
    /// </summary>
    public static WebApplication UseAnthropicEndpoints(
        this WebApplication app,
        ServerConfiguration config,
        ILogger? logger)
    {
        app.MapPost("/v1/messages", async (IChatCompletionService chatService, IModelRepository modelRepo, HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            bool isStreaming = requestBodyStr.Contains("\"stream\": true") ||
                              context.Request.Headers.ContainsKey("X-Stream");

            try
            {
                if (isStreaming)
                {
                    var result = await ProcessChatCompletion(context, requestBodyStr, true, config, logger);
                    return; // Streaming handled internally by this method
                }

                var anthropicRequest = System.Text.Json.JsonSerializer.Deserialize<AnthropicRequest>(requestBodyStr);

                if (anthropicRequest == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Failed to parse Anthropic request" });
                    return;
                }

                if (chatService != null)
                {
                    var messages = new List<Message>();
                    string? systemMessage = null;
                    try
                    {
                        var jsonBody = System.Text.Json.JsonSerializer.Deserialize<AnthropicRequest>(requestBodyStr);
                        systemMessage = jsonBody?.System;
                    }
                    catch { /* Ignore parse errors */ }

                    foreach (var message in (anthropicRequest.Messages ?? []).ToArray())
                    {
                        var textContent = message.Content;

                        if (!string.IsNullOrEmpty(textContent))
                        {
                            messages.Add(new Message
                            {
                                Role = message.Role == "user" ? MessageRole.User : MessageRole.Assistant,
                                Content = textContent,
                                TokenCount = Math.Max(1, EstimateTokenCount(textContent))
                            });
                        }
                    }

                    if (!string.IsNullOrEmpty(systemMessage))
                    {
                        messages.Insert(0, new Message
                        {
                            Role = MessageRole.System,
                            Content = systemMessage,
                            TokenCount = Math.Max(1, EstimateTokenCount(systemMessage))
                        });
                    }

                    var chatReq = new ChatRequest(
                        anthropicRequest.Model ?? "local-model",
                        messages,
                        (double?)(anthropicRequest.Temperature ?? 0.7),
                        anthropicRequest.MaxTokens > 0 ? (int?)anthropicRequest.MaxTokens : null,
                        (double?)(anthropicRequest.TopP ?? 1.0));

                    var responseChoice = await chatService.GetCompletionAsync(chatReq);

                    var inputTokenCount = messages.Sum(m => m.TokenCount > 0 ? m.TokenCount : EstimateTokenCount(m.Content));
                    int outputTokenCount;
                    if (!string.IsNullOrEmpty(responseChoice.Message.Content))
                        outputTokenCount = responseChoice.Message.TokenCount > 0 ? responseChoice.Message.TokenCount : EstimateTokenCount(responseChoice.Message.Content);
                    else
                        outputTokenCount = 0;

                    var response = new
                    {
                        id = $"msg_{Guid.NewGuid():N}",
                        type = "message",
                        role = "assistant",
                        content = new[] { new {
                            type = "text",
                            text = responseChoice.Message.Content ?? "[No response]"
                        } },
                        model = anthropicRequest.Model,
                        stop_reason = string.IsNullOrEmpty(responseChoice.FinishReason) ? "end_turn" : responseChoice.FinishReason.ToLowerInvariant(),
                        usage = new
                        {
                            input_tokens = inputTokenCount,
                            output_tokens = outputTokenCount,
                            total_tokens = inputTokenCount + outputTokenCount
                        }
                    };

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(response);
                }
                else
                {
                    var modelIds = new List<string>();
                    if (modelRepo != null)
                    {
                        try
                        {
                            var models = await modelRepo.DiscoverModelsAsync();
                            modelIds = models.Select(m => m.Id.ToString()).ToList();
                        }
                        catch { /* Ignore errors */ }
                    }

                    var response = new
                    {
                        id = $"msg_{Guid.NewGuid():N}",
                        type = "message",
                        role = "assistant",
                        content = new[] { new {
                            type = "text",
                            text = "[No chat completion service configured]"
                        } },
                        model = anthropicRequest.Model,
                        stop_reason = "end_turn",
                        usage = new { input_tokens = 0, output_tokens = 0 },
                        models_available = modelIds
                    };

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(response);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing Anthropic message request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        // Embeddings endpoint
        app.MapPost("/v1/embeddings", async (IEmbeddingPipelineService pipeline, IModelRepository modelRepo, HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            try
            {
                var embeddingsRequest = System.Text.Json.JsonSerializer.Deserialize<EmbeddingsRequest>(requestBodyStr);

                if (embeddingsRequest == null || string.IsNullOrEmpty(embeddingsRequest.Model))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Model identifier and input are required" });
                    return;
                }

                var modelType = await DetectModelTypeAsync(logger, modelRepo, embeddingsRequest.Model);
                if (modelType != ModelType.Embedding && modelType != ModelType.TextGeneration)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "model_type_not_supported_for_embeddings",
                        message = $"Model '{embeddingsRequest.Model}' is not an embedding model and cannot be used with /v1/embeddings.",
                        suggested_endpoint = "/v1/chat/completions"
                    });
                    return;
                }

                var inputs = embeddingsRequest.Input switch
                {
                    string s => new[] { s },
                    System.Text.Json.JsonElement[] arr => arr.Select(e => e.GetString() ?? "").ToArray(),
                    _ => throw new InvalidOperationException("Input must be a string or array of strings")
                };

                float[][] embeddingVectors;

                if (inputs.Length == 1)
                {
                    var vector = await pipeline.GenerateAsync(embeddingsRequest.Model, inputs[0]);
                    embeddingVectors = new[] { vector };
                }
                else
                {
                    embeddingVectors = await pipeline.GenerateBatchAsync(embeddingsRequest.Model, inputs.ToList());
                }

                var data = new List<object>();
                for (var i = 0; i < embeddingVectors.Length; i++)
                {
                    data.Add(new
                    {
                        @object = "embedding",
                        index = i,
                        embedding = embeddingVectors[i]
                    });
                }

                var response = new
                {
                    @object = "list",
                    model = embeddingsRequest.Model,
                    usage = new
                    {
                        prompt_tokens = inputs.Sum(s => s?.Length / 4 + 3 / 4),
                        total_tokens = embeddingVectors.Length
                    },
                    data
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing embedding request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        return app;
    }

    /// <summary>
    /// Registers model listing endpoints (/v1/models/image/list, /v1/models/vae/list, /v1/models/lora/list, etc.).
    /// </summary>
    public static WebApplication UseModelListEndpoints(
        this WebApplication app,
        ServerConfiguration config,
        ILogger? logger)
    {
        // Image generation models
        app.MapGet("/v1/models/image/list", async (IModelRepository repo, HttpContext context) =>
        {
            try
            {
                var models = await repo.SearchMultiModalModelsAsync(modelTypeFilter: Domain.Models.ModelType.ImageGeneration);

                var modelInfos = new List<object>();
                foreach (var model in models)
                {
                    modelInfos.Add(new
                    {
                        id = model.Id,
                        obj = "model",
                        owned_by = "local",
                        display_name = model.Name,
                        model_type = "image_generation",
                        format = model.Format.ToString()
                    });
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new { obj = "list", data = modelInfos });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error listing image generation models");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list image models" });
            }
        });

        // Embedding models
        app.MapGet("/v1/models/embedding/list", async (IModelRepository repo, HttpContext context) =>
        {
            try
            {
                var models = await repo.SearchMultiModalModelsAsync(modelTypeFilter: Domain.Models.ModelType.Embedding);

                var modelInfos = new List<object>();
                foreach (var model in models)
                {
                    modelInfos.Add(new
                    {
                        id = model.Id,
                        obj = "model",
                        owned_by = "local",
                        display_name = model.Name,
                        model_type = "embedding",
                        format = model.Format.ToString()
                    });
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new { obj = "list", data = modelInfos });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error listing embedding models");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list embedding models" });
            }
        });

        // Upscaling models
        app.MapGet("/v1/models/image/upscaling/list", async (IModelRepository repo, HttpContext context) =>
        {
            try
            {
                var models = await repo.SearchMultiModalModelsAsync(modelTypeFilter: Domain.Models.ModelType.ImageGeneration);

                var upscaleModels = models.Where(m =>
                    m.Name != null && (m.Name.Contains("upscaler", StringComparison.OrdinalIgnoreCase) ||
                                       m.PipelineType?.Equals("esrgan", StringComparison.OrdinalIgnoreCase) == true));

                var modelInfos = new List<object>();
                foreach (var model in upscaleModels)
                {
                    modelInfos.Add(new
                    {
                        id = model.Id,
                        obj = "model",
                        owned_by = "local",
                        display_name = model.Name,
                        model_type = "upscaling",
                        format = model.Format.ToString()
                    });
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new { obj = "list", data = modelInfos });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error listing upscaling models");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list upscaling models" });
            }
        });

        // VAE models
        app.MapGet("/v1/models/vae/list", async (IVAEPipelineService vaePipeline, HttpContext context) =>
        {
            try
            {
                var models = await vaePipeline.GetAvailableModelsAsync();

                var modelInfos = new List<object>();
                foreach (var model in models)
                {
                    modelInfos.Add(new
                    {
                        id = model.Id,
                        obj = "model",
                        owned_by = "local",
                        display_name = model.Name,
                        model_type = "vae"
                    });
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new { obj = "list", data = modelInfos });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error listing VAE models");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list VAE models" });
            }
        });

        // LoRA adapters
        app.MapGet("/v1/models/lora/list", async (ILoraAdapterManager loraManager, HttpContext context) =>
        {
            try
            {
                var models = await loraManager.GetAvailableAdaptersAsync();

                var modelInfos = new List<object>();
                foreach (var model in models)
                {
                    modelInfos.Add(new
                    {
                        id = model.Id,
                        obj = "model",
                        owned_by = "local",
                        display_name = model.Name,
                        model_type = "lora_adapter",
                        format_variant = model.Format.ToString()
                    });
                }

                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new { obj = "list", data = modelInfos });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error listing LoRA adapters");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Failed to list LoRA adapters" });
            }
        });

        return app;
    }

    /// <summary>
    /// Registers image generation endpoints (/v1/images/*).
    /// </summary>
    public static WebApplication UseImageEndpoints(
        this WebApplication app,
        ServerConfiguration config,
        ILogger? logger)
    {
        // Image generations
        app.MapPost("/v1/images/generations", async (IDiffusionPipelineService pipeline, HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            try
            {
                var request = System.Text.Json.JsonSerializer.Deserialize<ImageGenerationRequest>(requestBodyStr);

                if (request == null || string.IsNullOrEmpty(request.ModelId))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Model identifier and prompt are required" });
                    return;
                }

                var streamProgress = false;
                if (context.Request.Headers.TryGetValue("X-Stream", out var streamHeader))
                {
                    streamProgress = string.Equals(streamHeader, "true", StringComparison.OrdinalIgnoreCase);
                }

                if (streamProgress)
                {
                    await HandleImageGenerationStreaming(context, pipeline, request);
                    return;
                }

                var result = await pipeline.GenerateImageAsync(request);

                var imageDataList = new List<object>
                {
                    new
                    {
                        b64_json = Convert.ToBase64String(result.ImageBytes),
                        width = result.Width,
                        height = result.Height,
                        seed = result.Seed
                    }
                };

                var response = new
                {
                    data = imageDataList,
                    @object = "list",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing image generation request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        // Inpainting
        app.MapPost("/v1/images/inpainting", async (IDiffusionPipelineService pipeline, HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            try
            {
                var request = System.Text.Json.JsonSerializer.Deserialize<ImageInpaintingRequest>(requestBodyStr);

                if (request == null || string.IsNullOrEmpty(request.ModelId) || string.IsNullOrEmpty(request.InitImage))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Model identifier and init image are required" });
                    return;
                }

                var result = await pipeline.GenerateInpaintingAsync(request);

                var imageDataList = new List<object>
                {
                    new
                    {
                        b64_json = Convert.ToBase64String(result.ImageBytes),
                        width = result.Width,
                        height = result.Height,
                        seed = result.Seed
                    }
                };

                var response = new
                {
                    data = imageDataList,
                    @object = "list",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing inpainting request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        // Outpainting
        app.MapPost("/v1/images/outpainting", async (IDiffusionPipelineService pipeline, HttpContext context) =>
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var requestBodyStr = await reader.ReadToEndAsync();

            try
            {
                var request = System.Text.Json.JsonSerializer.Deserialize<ImageOutpaintingRequest>(requestBodyStr);

                if (request == null || string.IsNullOrEmpty(request.ModelId) || string.IsNullOrEmpty(request.InitImage))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Model identifier and init image are required" });
                    return;
                }

                var result = await pipeline.GenerateOutpaintingAsync(request);

                var imageDataList = new List<object>
                {
                    new
                    {
                        b64_json = Convert.ToBase64String(result.ImageBytes),
                        width = result.Width,
                        height = result.Height,
                        seed = result.Seed
                    }
                };

                var response = new
                {
                    data = imageDataList,
                    @object = "list",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing outpainting request");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
            }
        });

        return app;
    }

    /// <summary>
    /// Registers health check endpoint (/v1/health).
    /// </summary>
    public static WebApplication UseHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/v1/health", () => Results.Ok(new
        {
            status = "healthy",
        }));
        return app;
    }

    // ---- Helpers used by extension methods ----

    private static async Task HandleStreamingResponse(HttpContext context, ServerConfiguration config, ILogger? logger)
    {
        using var reader = new StreamReader(context.Request.Body);
        var requestBodyStr = await reader.ReadToEndAsync();

        try
        {
            var requestId = Guid.NewGuid().ToString("N");
            var connectionId = Guid.NewGuid().ToString("N")[..16];

            string? reconnectFromEventId = null;
            if (context.Request.Headers.TryGetValue("Last-Event-ID", out var lastEventId))
            {
                reconnectFromEventId = lastEventId.ToString();
                logger?.LogInformation("SSE reconnection detected from event ID: {EventId}", reconnectFromEventId);
            }

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.Append("Cache-Control", "no-cache");
            context.Response.Headers.Append("Connection", "keep-alive");
            context.Response.Headers.Append("X-Event-ID", connectionId);

            var cts = new CancellationTokenSource();

            try
            {
                IChatCompletionService? chatService = context.RequestServices.GetRequiredService<IChatCompletionService>();
                var request = ParseStreamingRequest(requestBodyStr, requestId);

                if (request == null)
                {
                    await WriteSseError(context, "Failed to parse request");
                    return;
                }

                logger?.LogInformation("SSE streaming started for model: {ModelId}, request: {RequestId}",
                    request.ModelId, requestId);

                var totalPromptTokens = 0L;
                var totalCompletionTokens = 0L;
                bool firstChunk = true;

                await foreach (var chunk in chatService.GetStreamingCompletionAsync(request).WithCancellation(cts.Token))
                {
                    if (cts.IsCancellationRequested) break;

                    var token = ExtractTokenFromSseChunk(chunk);

                    if (firstChunk)
                    {
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
                cts.Dispose();
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger?.LogDebug("SSE stream cancelled for request");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in SSE streaming");
            await WriteSseError(context, "Streaming error: " + ex.Message);
        }
    }

    private static async Task<object?> ProcessChatCompletion(HttpContext context, string requestBodyStr, bool isStreaming, ServerConfiguration config, ILogger? logger)
    {
        try
        {
            IChatCompletionService? chatService = context.RequestServices.GetRequiredService<IChatCompletionService>();
            var request = ParseChatRequest(requestBodyStr);

            if (request == null)
            {
                context.Response.StatusCode = 400;
                return new { error = "Failed to parse chat completion request" };
            }

            List<Message> messages;

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

            var modelRepo = context.RequestServices.GetService<IModelRepository>();
            var modelId = request.ModelId ?? "local-model";
            if (modelRepo != null)
            {
                var modelType = await DetectModelTypeAsync(logger, modelRepo, modelId);
                if (modelType.HasValue && modelType.Value != ModelType.TextGeneration)
                {
                    context.Response.StatusCode = 400;
                    await WriteChatCompletionNotSupportedError(context, modelId);
                    return null;
                }
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
                return null;

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
            logger?.LogError(ex, "Error processing chat completion request");
            context.Response.StatusCode = 500;
            return new { error = "Internal server error" };
        }
    }

    private static async Task HandleImageGenerationStreaming(HttpContext context, IDiffusionPipelineService pipeline, ImageGenerationRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var connectionId = Guid.NewGuid().ToString("N")[..16];

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Event-ID", connectionId);

        var cts = new CancellationTokenSource();

        try
        {
            await WriteSseEvent(context, requestId, "generation_start", new
            {
                id = $"img_{requestId}",
                @object = "image.generation.chunk",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = request.ModelId,
                prompt = request.Prompt
            });

            var progressTask = Task.Run(async () =>
            {
                await foreach (var progress in pipeline.StreamProgressAsync(request).WithCancellation(cts.Token))
                {
                    try
                    {
                        await WriteSseEvent(context, requestId, "progress", new
                        {
                            step = progress.Step,
                            total_steps = progress.TotalSteps,
                            percentage = Math.Round(progress.ProgressPercent, 1)
                        });
                        await context.Response.Body.FlushAsync(cts.Token);
                    }
                    catch { /* Ignore cancellation during streaming */ }
                }
            }, cts.Token);

            var imageTask = pipeline.GenerateImageAsync(request, cts.Token);

            await Task.WhenAll(progressTask, imageTask);

            if (imageTask.IsCompletedSuccessfully && imageTask.Result != null)
            {
                try
                {
                    cts.Cancel();
                    var result = imageTask.Result;

                    await WriteSseEvent(context, requestId, "generation_complete", new
                    {
                        id = $"img_{requestId}",
                        @object = "image.generation.chunk",
                        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        model = request.ModelId,
                        data = new[]
                        {
                            new
                            {
                                b64_json = Convert.ToBase64String(result.ImageBytes),
                                width = result.Width,
                                height = result.Height,
                                seed = result.Seed
                            }
                        }
                    });
                }
                catch { /* Ignore errors during final event streaming */ }
            }
            else if (imageTask.IsFaulted)
            {
                var ex = imageTask.Exception?.InnerException ?? new Exception("Image generation failed");
                await WriteSseError(context, $"Image generation failed: {ex.Message}");
            }
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static ChatRequest? ParseChatRequest(string requestBodyStr)
    {
        try
        {
            var parsedBody = System.Text.Json.JsonSerializer.Deserialize<JsonChatRequest>(requestBodyStr);

            if (parsedBody == null) return null;

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
            return new ChatRequest(
                "local-model",
                new List<Message> { new Message { Role = MessageRole.User, Content = "[No content provided]", TokenCount = 10 } },
                (double?)0.7);
        }
    }

    private static ChatRequest? ParseStreamingRequest(string requestBodyStr, string requestId)
    {
        try
        {
            var parsedBody = System.Text.Json.JsonSerializer.Deserialize<JsonChatRequest>(requestBodyStr);

            if (parsedBody == null) return null;

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
        catch (System.Text.Json.JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse streaming request body — returning null to let caller handle the error");
            return null;
        }
    }

    private static string ExtractTokenFromSseChunk(string chunk)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Deserialize<JsonSseChunk>(chunk);

            if (json == null || string.IsNullOrEmpty(json.Token))
                return string.Empty;

            return json.Token;
        }
        catch
        {
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

            var trimChars = new[] { '{', '}', '"', '\'' };
            return chunk.Trim(trimChars).Trim();
        }
    }

    private static async Task WriteSseEvent(HttpContext context, string requestId, string eventType, object data)
    {
        var eventStr = $"event: {eventType}\ndata: {System.Text.Json.JsonSerializer.Serialize(data)}\n\n";
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(eventStr));
    }

    private static async Task WriteSseError(HttpContext context, string error)
    {
        object errorData = new { error };
        await WriteSseEvent(context, "", "error", errorData);
    }

    private static async Task WriteChatCompletionNotSupportedError(HttpContext context, string modelId)
    {
        var suggestedEndpoint = "/v1/embeddings";

        if (modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase))
            suggestedEndpoint = "/v1/embeddings";
        else if (modelId.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("diffusion", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("sdxl", StringComparison.OrdinalIgnoreCase) ||
                 modelId.Contains("flux", StringComparison.OrdinalIgnoreCase))
            suggestedEndpoint = "/v1/images/generations";

        await context.Response.WriteAsJsonAsync(new
        {
            error = "model_type_not_supported_for_chat_completion",
            message = $"Model '{modelId}' is not a text generation model and cannot be used with /v1/chat/completions.",
            suggested_endpoint = suggestedEndpoint,
            details = new[]
            {
                "Chat completion (text generation) models use GGUF format. Image generation, diffusion, VAE, LoRA, and embedding models require their respective endpoints."
            }
        });
    }

    private static int EstimateTokenCount(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    private static async Task<ModelType?> DetectModelTypeAsync(ILogger? logger, IModelRepository? repo, string modelId)
    {
        if (repo == null)
            return null;

        var multimodal = await repo.GetMultiModalModelByIdAsync(modelId);
        if (multimodal != null)
            return multimodal.ModelType;

        logger?.LogDebug("Model '{ModelId}' resolved as GGUF text generation model", modelId);
        return ModelType.TextGeneration;
    }
}
