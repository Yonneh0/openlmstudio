using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Benchmarking;

/// <summary>
/// Benchmarks chat completion performance (token generation throughput, context compression).
/// </summary>
public class ChatCompletionBenchmark
{
    private readonly ILogger<ChatCompletionBenchmark>? _logger;

    public ChatCompletionBenchmark(ILogger<ChatCompletionBenchmark>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Measures token generation throughput (tokens per second).
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkTokenThroughputAsync(
        IChatCompletionService chatCompletionService,
        int promptTokens = 100,
        int completionTokens = 50,
        int iterations = 3,
        CancellationToken ct = default)
    {
        var throughputs = new List<double>();

        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var response = await chatCompletionService.GetCompletionAsync(new ChatRequest("default", new List<Message>
            {
                new Message { Role = MessageRole.User, Content = new string('A', promptTokens) }
            }, 0.7f, completionTokens, 1.0, false));

            sw.Stop();

            if (response != null && sw.ElapsedMilliseconds > 0)
            {
                var tokensPerSecond = (completionTokens * 1000.0) / sw.ElapsedMilliseconds;
                throughputs.Add(tokensPerSecond);
            }
        }

        return new BenchmarkResult("Token Generation Throughput", MeanTokensPerSecond: throughputs.Average(), MinTokensPerSecond: throughputs.Min(), MaxTokensPerSecond: throughputs.Max(), StdDevTokensPerSecond: StandardDeviation(throughputs), Iterations: iterations);
    }

    /// <summary>
    /// Measures context compression time for various context sizes.
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkContextCompressionAsync(
        IContextCompressor compressor,
        int segmentCount,
        int iterations = 3,
        CancellationToken ct = default)
    {
        var compressionTimes = new List<double>();
        var tokenSavings = new List<double>();

        for (int i = 0; i < iterations; i++)
        {
            var segments = new List<ContextSegment>();
            var tokenCount = 0;

            for (int j = 0; j < segmentCount; j++)
            {
                var content = $"Message {j}: This is a simulated conversation message with content that contributes to the token count.";
                var seg = new ContextSegment
                {
                    Content = content,
                    InjectionType = ContextInjectionType.CompressedHistory,
                    TokenCount = (int)(content.Length / 4.0),
                };
                segments.Add(seg);
                tokenCount += seg.TokenCount;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var compressed = await compressor.CompressAsync(segments, CompressionLevel.Medium);
            sw.Stop();

            compressionTimes.Add(sw.ElapsedMilliseconds);
            tokenSavings.Add((1.0 - (compressed.CompressedSegments.Sum(s => s.TokenCount) / (double)tokenCount)) * 100);
        }

        return new BenchmarkResult($"Context Compression ({segmentCount} segments)", MeanCompressionMs: compressionTimes.Average(), MinCompressionMs: compressionTimes.Min(), MaxCompressionMs: compressionTimes.Max(), StdDevCompressionMs: StandardDeviation(compressionTimes), MeanTokenSavingsPercent: tokenSavings.Average(), Iterations: iterations);
    }

    private static double StandardDeviation(IEnumerable<double> values)
    {
        var arr = values.ToArray();
        if (arr.Length < 2) return 0;
        var mean = arr.Average();
        var variance = arr.Select(v => Math.Pow(v - mean, 2)).Sum() / arr.Length;
        return Math.Sqrt(variance);
    }
}

/// <summary>
/// Holds the results of a single benchmark run.
/// </summary>
public record BenchmarkResult(
    string Name,
    double? MeanLoadMs = null,
    double? MinLoadMs = null,
    double? MaxLoadMs = null,
    double? StdDevLoadMs = null,
    double? MeanUnloadMs = null,
    double? MinUnloadMs = null,
    double? MaxUnloadMs = null,
    double? StdDevUnloadMs = null,
    double? MeanTokensPerSecond = null,
    double? MinTokensPerSecond = null,
    double? MaxTokensPerSecond = null,
    double? StdDevTokensPerSecond = null,
    double? MeanCompressionMs = null,
    double? MinCompressionMs = null,
    double? MaxCompressionMs = null,
    double? StdDevCompressionMs = null,
    double? MeanTokenSavingsPercent = null,
    int Iterations = 0);