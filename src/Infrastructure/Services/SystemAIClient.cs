using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.SystemAI;
using System.Text;
using System.Text.Json;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Chat message for HTTP-based System AI communication.
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "";
    public string? Content { get; set; }
}

/// <summary>
/// Request for chat completions to the llama.cpp server.
/// Renamed from ChatRequest to avoid collision with the ChatRequest record in IChatCompletionService.cs.
/// </summary>
public class SystemAIChatRequest
{
    public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
    public bool Stream { get; set; }
    public float Temperature { get; set; }
    public float TopP { get; set; }
}

/// <summary>
/// Delta from streaming response.
/// </summary>
public class Delta
{
    public string? Content { get; set; }
}

/// <summary>
/// Choice from streaming response.
/// </summary>
public class Choice
{
    public Delta Delta { get; set; } = new();
}

/// <summary>
/// Streaming response from llama.cpp server.
/// </summary>
public class StreamingResponse
{
    public Choice[] Choices { get; set; } = Array.Empty<Choice>();
}

/// <summary>
/// Client for the System AI (llama.cpp) inference engine.
/// Spawns llama-server process and communicates via HTTP POST streaming.
/// </summary>
public class SystemAIClient : ISystemAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SystemAIClient> _logger;
    private readonly SystemAIConfig _config;
    private Process? _process;
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<SseChunk>? OnChunk;
    public event EventHandler<SseDone>? OnDone;
    public event EventHandler<string>? OnError;

    public SystemAIClient(ILogger<SystemAIClient> logger, SystemAIConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new SystemAIConfig();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<bool> StartAsync()
    {
        lock (_lock)
        {
            if (_process is { HasExited: false })
                return true;
        }

        var args = $"--mlock {_config.MemoryLock} -m \"{_config.ModelPath}\" --port {_config.Port}";
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "llama-server",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            _process = proc;
            _logger.LogInformation("Started llama-server on port {Port} for System AI", _config.Port);
            return await WaitForServerReady(_config.Port).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start llama-server for System AI");
            return false;
        }
    }

    public async Task<string?> SendMessageAsync(string message, string? compressedContext = null)
    {
        if (_process is null or { HasExited: true })
        {
            if (!await StartAsync().ConfigureAwait(false))
            {
                OnError?.Invoke(this, "System AI server not running.");
                return null;
            }
        }

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = _config.SystemPrompt }
        };

        if (!string.IsNullOrEmpty(compressedContext))
            messages.Add(new() { Role = "system", Content = $"Compressed context:\n{compressedContext}" });

        messages.Add(new() { Role = "user", Content = message });

        var body = new SystemAIChatRequest
        {
            Messages = messages.ToArray(),
            Stream = true,
            Temperature = _config.Temperature,
            TopP = _config.TopP,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(body),
            System.Text.Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.PostAsync(
                $"http://127.0.0.1:{_config.Port}/v1/chat/completions",
                content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                OnError?.Invoke(this, $"Server returned {response.StatusCode}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var sb = new StringBuilder();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line?.StartsWith("data: ") == true)
                {
                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]")
                    {
                        OnDone?.Invoke(this, new SseDone());
                        break;
                    }

                    try
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
                        using var doc = JsonDocument.Parse(bytes);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0];
                            if (delta.TryGetProperty("delta", out var deltaProp) && deltaProp.TryGetProperty("content", out var contentProp))
                            {
                                var text = contentProp.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    sb.Append(text);
                                    OnChunk?.Invoke(this, new SseChunk(text));
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse SSE chunk");
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to System AI");
            OnError?.Invoke(this, ex.Message);
            return null;
        }
    }

    public async Task StopAsync()
    {
        lock (_lock)
        {
            if (_process is null) return;
            var proc = _process;
            _process = null;
            try
            {
                proc.Kill();
            }
            catch { /* ignore */ }
        }
        await Task.Delay(2000).ConfigureAwait(false);
        lock (_lock)
        {
            if (_process is { HasExited: false })
                _process.Kill(true);
        }
    }

    private static async Task<bool> WaitForServerReady(int port)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync($"http://127.0.0.1:{port}/health").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch { /* server not ready yet */ }
            await Task.Delay(500).ConfigureAwait(false);
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { StopAsync().Wait(); } catch { /* ignore */ }
        _httpClient.Dispose();
    }
}