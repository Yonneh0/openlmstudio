using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Benchmarks performance of OpenLMStudio services (chat, image generation, model loading).
/// </summary>
public class PerformanceBenchmarkService
{
    private readonly ILogger<PerformanceBenchmarkService>? _logger;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly IDiffusionPipelineService? _imagePipeline;

    public PerformanceBenchmarkService(
        ILogger<PerformanceBenchmarkService>? logger,
        IChatCompletionService? chatCompletion = null,
        IDiffusionPipelineService? imagePipeline = null)
    {
        _logger = logger;
        _chatCompletion = chatCompletion;
        _imagePipeline = imagePipeline;
    }

    /// <summary>
    /// Benchmarks model loading time by loading a model and measuring the elapsed time.
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkModelLoadAsync(string modelId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_imagePipeline != null)
            {
                await _imagePipeline.LoadModelAsync(modelId);
            }
            sw.Stop();
            return new BenchmarkResult
            {
                BenchmarkName = "ModelLoad",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                ModelLoadTimeMs = sw.ElapsedMilliseconds,
                Succeeded = true,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new BenchmarkResult
            {
                BenchmarkName = "ModelLoad",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                ModelLoadTimeMs = sw.ElapsedMilliseconds,
                Succeeded = false,
                Error = ex.Message,
            };
        }
    }

    /// <summary>
    /// Benchmarks chat completion by sending a prompt and measuring token throughput.
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkChatCompletionAsync(
        string modelId, string prompt, int maxTokens, int iterations, CancellationToken ct = default)
    {
        if (_chatCompletion == null)
            return new BenchmarkResult
            {
                BenchmarkName = "ChatCompletion",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                DurationMs = 0,
                Succeeded = false,
                Error = "IChatCompletionService not registered in DI container.",
            };

        var latencies = new List<double>();
        var totalTokens = 0;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var stepSw = Stopwatch.StartNew();
            var messages = new List<Message>
            {
                new Message { Role = MessageRole.User, Content = prompt },
            };

            var result = await _chatCompletion.GetCompletionAsync(new ChatRequest(modelId, messages, MaxTokens: maxTokens));
            stepSw.Stop();

            var tokens = result.Message.Content?.Length ?? 0;
            latencies.Add(stepSw.ElapsedMilliseconds);
            totalTokens += tokens;
        }

        sw.Stop();
        var avgLatency = latencies.Average();
        var sorted = latencies.OrderBy(x => x).ToList();
        var p50 = sorted[(int)(sorted.Count * 0.5)];
        var p95 = sorted[(int)(sorted.Count * 0.95)];
        var p99 = sorted[Math.Min((int)(sorted.Count * 0.99), sorted.Count - 1)];

        return new BenchmarkResult
        {
            BenchmarkName = "ChatCompletion",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            DurationMs = sw.ElapsedMilliseconds,
            Succeeded = true,
            TokensPerSecond = sw.ElapsedMilliseconds > 0 ? totalTokens / (sw.ElapsedMilliseconds / 1000.0) : 0,
            AvgLatencyMs = avgLatency,
            P50LatencyMs = p50,
            P95LatencyMs = p95,
            P99LatencyMs = p99,
        };
    }

    /// <summary>
    /// Benchmarks image generation latency across multiple iterations.
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkImageGenerationAsync(
        string modelId, int width, int height, int steps, int iterations, CancellationToken ct = default)
    {
        if (_imagePipeline == null)
            return new BenchmarkResult
            {
                BenchmarkName = "ImageGeneration",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                DurationMs = 0,
                Succeeded = false,
                Error = "IDiffusionPipelineService not registered in DI container.",
            };

        var latencies = new List<double>();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var stepSw = Stopwatch.StartNew();
            var request = new ImageGenerationRequest(
                modelId, "benchmark test", "blurry", width, height, 7.5, steps, -1);

            var result = await _imagePipeline.GenerateImageAsync(request, ct);
            stepSw.Stop();

            latencies.Add(stepSw.ElapsedMilliseconds);
        }

        sw.Stop();
        var avgLatency = latencies.Average();
        var sorted = latencies.OrderBy(x => x).ToList();
        var p50 = sorted[(int)(sorted.Count * 0.5)];
        var p95 = sorted[(int)(sorted.Count * 0.95)];
        var p99 = sorted[Math.Min((int)(sorted.Count * 0.99), sorted.Count - 1)];

        return new BenchmarkResult
        {
            BenchmarkName = "ImageGeneration",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            DurationMs = sw.ElapsedMilliseconds,
            Succeeded = true,
            AvgLatencyMs = avgLatency,
            P50LatencyMs = p50,
            P95LatencyMs = p95,
            P99LatencyMs = p99,
            PeakMemoryMb = GetPeakMemoryMb(),
        };
    }

    /// <summary>
    /// Runs a full benchmark suite: model load, chat, and image generation.
    /// </summary>
    public async Task<BenchmarkResult> RunFullBenchmarkAsync(
        string modelId, string chatPrompt, CancellationToken ct = default)
    {
        var results = new List<BenchmarkResult>();

        // 1. Model load
        var loadResult = await BenchmarkModelLoadAsync(modelId, ct);
        results.Add(loadResult);

        // 2. Chat completion (warm-up)
        var chatResult = await BenchmarkChatCompletionAsync(modelId, chatPrompt, 256, 3, ct);
        results.Add(chatResult);

        // 3. Image generation
        var imageResult = await BenchmarkImageGenerationAsync(modelId, 512, 512, 20, 2, ct);
        results.Add(imageResult);

        // Aggregate
        var totalMs = results.Sum(r => r.DurationMs);
        var totalTokens = results.Sum(r => (int)r.TokensPerSecond);
        var peakMem = results.Max(r => r.PeakMemoryMb);

        return new BenchmarkResult
        {
            BenchmarkName = "FullBenchmark",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            DurationMs = totalMs,
            ModelLoadTimeMs = loadResult.ModelLoadTimeMs,
            TokensPerSecond = totalTokens / (totalMs / 1000.0),
            AvgLatencyMs = imageResult.AvgLatencyMs,
            P50LatencyMs = imageResult.P50LatencyMs,
            P95LatencyMs = imageResult.P95LatencyMs,
            P99LatencyMs = imageResult.P99LatencyMs,
            PeakMemoryMb = peakMem,
            Succeeded = results.All(r => r.Succeeded),
        };
    }

    private double GetPeakMemoryMb()
    {
        var mem = System.GC.GetTotalMemory(false);
        return mem / (1024.0 * 1024.0);
    }
}