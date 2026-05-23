using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Types;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Performs load testing of OpenLMStudio server endpoints under concurrent requests.
/// </summary>
public class ServerLoadTestService
{
    private readonly ILogger<ServerLoadTestService>? _logger;
    private readonly HttpClient _httpClient;

    public ServerLoadTestService(ILogger<ServerLoadTestService>? logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Runs concurrent requests against the chat completions endpoint.
    /// </summary>
    public async Task<LoadTestResult> RunChatLoadTestAsync(
        string baseUrl, int totalRequests, int concurrentRequests, CancellationToken ct = default)
    {
        var result = LoadTestResult.Create("ChatLoadTest");
        var latencies = new List<double>();
        var errors = new List<string>();
        var tasks = new List<Task>();
        var sw = Stopwatch.StartNew();

        var chunks = totalRequests / concurrentRequests;
        if (chunks == 0) chunks = 1;

        for (int chunk = 0; chunk < concurrentRequests && !ct.IsCancellationRequested; chunk++)
        {
            for (int i = 0; i < chunks && i < totalRequests; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var stepSw = Stopwatch.StartNew();
                    try
                    {
                        var response = await _httpClient.PostAsJsonAsync(
                            $"{baseUrl.TrimEnd('/')}/v1/chat/completions",
                            new
                            {
                                model = "gpt-4",
                                messages = new[] { new { role = "user", content = "Say something interesting." } },
                                max_tokens = 256,
                                temperature = 0.7,
                            },
                            ct: ct);

                        stepSw.Stop();
                        latencies.Add(stepSw.ElapsedMilliseconds);

                        if (!response.IsSuccessStatusCode)
                        {
                            errors.Add($"Request {chunk * chunks + i}: HTTP {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Request {chunk * chunks + i}: {ex.Message}");
                    }
                }, ct));
            }
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        result.CompletedAt = DateTime.UtcNow;
        result.DurationMs = sw.ElapsedMilliseconds;
        result.TotalRequests = totalRequests;
        result.SuccessfulRequests = totalRequests - errors.Count;
        result.FailedRequests = errors.Count;
        result.Errors = errors;
        result.MinLatencyMs = latencies.Count > 0 ? latencies.Min() : 0;
        result.MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : 0;
        result.AvgLatencyMs = latencies.Count > 0 ? latencies.Average() : 0;
        var sorted = latencies.OrderBy(x => x).ToList();
        if (sorted.Count > 0)
        {
            result.P50LatencyMs = sorted[(int)(sorted.Count * 0.5)];
            result.P95LatencyMs = sorted[(int)(sorted.Count * 0.95)];
            result.P99LatencyMs = sorted[Math.Min((int)(sorted.Count * 0.99), sorted.Count - 1)];
        }

        _logger?.LogInformation("Chat load test complete: {Total} requests, {Successful} succeeded, {Failed} failed in {Duration:F1}ms",
            totalRequests, result.SuccessfulRequests, result.FailedRequests, result.DurationMs);

        return result;
    }

    /// <summary>
    /// Runs concurrent requests against the image generation endpoint.
    /// </summary>
    public async Task<LoadTestResult> RunImageLoadTestAsync(
        string baseUrl, int totalRequests, int concurrentRequests, CancellationToken ct = default)
    {
        var result = LoadTestResult.Create("ImageLoadTest");
        var latencies = new List<double>();
        var errors = new List<string>();
        var tasks = new List<Task>();

        for (int chunk = 0; chunk < concurrentRequests && !ct.IsCancellationRequested; chunk++)
        {
            for (int i = 0; i < totalRequests / concurrentRequests && i < totalRequests; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var stepSw = Stopwatch.StartNew();
                    try
                    {
                        var response = await _httpClient.PostAsJsonAsync(
                            $"{baseUrl.TrimEnd('/')}/v1/images/generations",
                            new
                            {
                                model = "sdxl",
                                prompt = "A beautiful landscape",
                                width = 512,
                                height = 512,
                                steps = 20,
                                guidance_scale = 7.5,
                            },
                            ct: ct);

                        stepSw.Stop();
                        latencies.Add(stepSw.ElapsedMilliseconds);

                        if (!response.IsSuccessStatusCode)
                        {
                            errors.Add($"Request {chunk * (totalRequests / concurrentRequests) + i}: HTTP {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Request {chunk * (totalRequests / concurrentRequests) + i}: {ex.Message}");
                    }
                }, ct));
            }
        }

        await Task.WhenAll(tasks);
        result.CompletedAt = DateTime.UtcNow;
        result.DurationMs = Stopwatch.StartNew().ElapsedMilliseconds;
        result.TotalRequests = totalRequests;
        result.SuccessfulRequests = totalRequests - errors.Count;
        result.FailedRequests = errors.Count;
        result.Errors = errors;
        result.MinLatencyMs = latencies.Count > 0 ? latencies.Min() : 0;
        result.MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : 0;
        result.AvgLatencyMs = latencies.Count > 0 ? latencies.Average() : 0;
        var sorted = latencies.OrderBy(x => x).ToList();
        if (sorted.Count > 0)
        {
            result.P50LatencyMs = sorted[(int)(sorted.Count * 0.5)];
            result.P95LatencyMs = sorted[(int)(sorted.Count * 0.95)];
            result.P99LatencyMs = sorted[Math.Min((int)(sorted.Count * 0.99), sorted.Count - 1)];
        }

        return result;
    }

    /// <summary>
    /// Runs concurrent requests against the messages endpoint.
    /// </summary>
    public async Task<LoadTestResult> RunMessagesLoadTestAsync(
        string baseUrl, int totalRequests, int concurrentRequests, CancellationToken ct = default)
    {
        var result = LoadTestResult.Create("MessagesLoadTest");
        var latencies = new List<double>();
        var errors = new List<string>();
        var tasks = new List<Task>();

        for (int chunk = 0; chunk < concurrentRequests && !ct.IsCancellationRequested; chunk++)
        {
            for (int i = 0; i < totalRequests / concurrentRequests && i < totalRequests; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var stepSw = Stopwatch.StartNew();
                    try
                    {
                        var response = await _httpClient.PostAsJsonAsync(
                            $"{baseUrl.TrimEnd('/')}/v1/messages",
                            new
                            {
                                messages = new[] { new { role = "user", content = "Hello, world!" } },
                            },
                            ct: ct);

                        stepSw.Stop();
                        latencies.Add(stepSw.ElapsedMilliseconds);

                        if (!response.IsSuccessStatusCode)
                        {
                            errors.Add($"Request {chunk * (totalRequests / concurrentRequests) + i}: HTTP {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Request {chunk * (totalRequests / concurrentRequests) + i}: {ex.Message}");
                    }
                }, ct));
            }
        }

        await Task.WhenAll(tasks);
        result.CompletedAt = DateTime.UtcNow;
        result.DurationMs = Stopwatch.StartNew().ElapsedMilliseconds;
        result.TotalRequests = totalRequests;
        result.SuccessfulRequests = totalRequests - errors.Count;
        result.FailedRequests = errors.Count;
        result.Errors = errors;
        result.MinLatencyMs = latencies.Count > 0 ? latencies.Min() : 0;
        result.MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : 0;
        result.AvgLatencyMs = latencies.Count > 0 ? latencies.Average() : 0;
        var sorted = latencies.OrderBy(x => x).ToList();
        if (sorted.Count > 0)
        {
            result.P50LatencyMs = sorted[(int)(sorted.Count * 0.5)];
            result.P95LatencyMs = sorted[(int)(sorted.Count * 0.95)];
            result.P99LatencyMs = sorted[Math.Min((int)(sorted.Count * 0.99), sorted.Count - 1)];
        }

        return result;
    }
}