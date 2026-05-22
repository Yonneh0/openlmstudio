using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Benchmarking;

/// <summary>
/// Load tests the local HTTP server endpoints under concurrent request scenarios.
/// </summary>
public class ServerLoadTest
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerLoadTest>? _logger;

    public ServerLoadTest(HttpClient httpClient, ILogger<ServerLoadTest>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends N concurrent requests to the chat completions endpoint and measures P50/P95/P99 latency.
    /// </summary>
    public async Task<LoadTestResult> LoadTestChatCompletionsAsync(
        int concurrentRequests = 10,
        int requestsPerWorker = 5,
        string? baseUrl = null,
        CancellationToken ct = default)
    {
        var url = baseUrl ?? "http://localhost:8080/v1/chat/completions";
        var allLatencies = new List<long>();
        var successes = 0;
        var failures = 0;

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(concurrentRequests);

        for (int w = 0; w < concurrentRequests; w++)
        {
            for (int r = 0; r < requestsPerWorker; r++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var response = await _httpClient.PostAsJsonAsync(url, new ChatCompletionRequest
                        {
                            Messages = new List<ChatMessage> { new() { Role = "user", Content = "Hello, this is a test message." } },
                            MaxTokens = 10,
                            Temperature = 0.7f
                        }, ct);

                        sw.Stop();
                        allLatencies.Add(sw.ElapsedMilliseconds);

                        if (response.IsSuccessStatusCode)
                            Interlocked.Increment(ref successes);
                        else
                            Interlocked.Increment(ref failures);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }
        }

        await Task.WhenAll(tasks);

        if (allLatencies.Count == 0)
            return new LoadTestResult { RequestCount = 0, Error = "No latencies recorded" };

        allLatencies.Sort();
        var total = allLatencies.Count;

        return new LoadTestResult
        {
            Endpoint = url,
            RequestCount = total,
            Successes = successes,
            Failures = failures,
            P50LatencyMs = allLatencies[total / 2],
            P95LatencyMs = allLatencies[total * 95 / 100],
            P99LatencyMs = allLatencies[total * 99 / 100],
            MinLatencyMs = allLatencies[0],
            MaxLatencyMs = allLatencies[total - 1],
            MeanLatencyMs = allLatencies.Average()
        };
    }

    /// <summary>
    /// Sends concurrent requests to the image generation endpoint.
    /// </summary>
    public async Task<LoadTestResult> LoadTestImageGenerationAsync(
        int concurrentRequests = 5,
        int requestsPerWorker = 3,
        string? baseUrl = null,
        CancellationToken ct = default)
    {
        var url = baseUrl ?? "http://localhost:8080/v1/images/generations";
        var allLatencies = new List<long>();
        var successes = 0;
        var failures = 0;

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(concurrentRequests);

        for (int w = 0; w < concurrentRequests; w++)
        {
            for (int r = 0; r < requestsPerWorker; r++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var response = await _httpClient.PostAsJsonAsync(url, new OpenAIImageGenerationRequest
                        {
                            Prompt = "A beautiful sunset",
                            Width = 512,
                            Height = 512,
                            Steps = 10
                        }, ct);

                        sw.Stop();
                        allLatencies.Add(sw.ElapsedMilliseconds);

                        if (response.IsSuccessStatusCode)
                            Interlocked.Increment(ref successes);
                        else
                            Interlocked.Increment(ref failures);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }
        }

        await Task.WhenAll(tasks);

        if (allLatencies.Count == 0)
            return new LoadTestResult { RequestCount = 0, Error = "No latencies recorded" };

        allLatencies.Sort();
        var total = allLatencies.Count;

        return new LoadTestResult
        {
            Endpoint = url,
            RequestCount = total,
            Successes = successes,
            Failures = failures,
            P50LatencyMs = allLatencies[total / 2],
            P95LatencyMs = allLatencies[total * 95 / 100],
            P99LatencyMs = allLatencies[total * 99 / 100],
            MinLatencyMs = allLatencies[0],
            MaxLatencyMs = allLatencies[total - 1],
            MeanLatencyMs = allLatencies.Average()
        };
    }
}

/// <summary>
/// Holds the results of a load test run.
/// </summary>
public record LoadTestResult(
    string? Endpoint = null,
    int RequestCount = 0,
    int Successes = 0,
    int Failures = 0,
    long? P50LatencyMs = null,
    long? P95LatencyMs = null,
    long? P99LatencyMs = null,
    long? MinLatencyMs = null,
    long? MaxLatencyMs = null,
    double? MeanLatencyMs = null,
    string? Error = null);