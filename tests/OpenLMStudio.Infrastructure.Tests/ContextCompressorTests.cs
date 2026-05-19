global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;

using NSubstitute;
using NUnit.Framework;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for context compression and relevance engine functionality.
/// </summary>
[TestFixture]
public class ContextCompressorTests
{
    private ConversationContextCompressor _compressor = null!;
    private ContextRelevanceEngine _engine = null!;

    [SetUp]
    public void Setup()
    {
        _compressor = new ConversationContextCompressor();
        _engine = new ContextRelevanceEngine();
    }

    [Test]
    public void CompressAsync_WithMediumCompression_ReturnsResult()
    {
        var segments = new List<ContextSegment>
        {
            ContextSegment.CreateUncompressed(new Message { Id = Guid.NewGuid(), Role = MessageRole.User, Content = "User message 1", TokenCount = 10 }),
            ContextSegment.CreateUncompressed(new Message { Id = Guid.NewGuid(), Role = MessageRole.Assistant, Content = "Assistant reply 1", TokenCount = 8 })
        };

        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await _compressor.CompressAsync(segments, CompressionLevel.Medium);
            Assert.That(result, Is.Not.Null);
        });
    }

    [Test]
    public void CompressAsync_WithHighCompression_ReturnsResult()
    {
        var segments = new List<ContextSegment>
        {
            ContextSegment.CreateUncompressed(new Message { Id = Guid.NewGuid(), Role = MessageRole.User, Content = "User message 1", TokenCount = 10 })
        };

        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await _compressor.CompressAsync(segments, CompressionLevel.Aggressive);
            Assert.That(result, Is.Not.Null);
        });
    }

    [Test]
    public void CompressAsync_WithEmptySegments_ReturnsEmptyResult()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await _compressor.CompressAsync([], CompressionLevel.Medium);
            Assert.That(result.TokensSaved, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public void ScoreSegmentsAsync_WithRecentMessages_ReturnsScores()
    {
        var segments = new List<ContextSegment>
        {
            ContextSegment.CreateEmptyWithRelevance(Guid.NewGuid()),
            ContextSegment.CreateEmptyWithRelevance(Guid.NewGuid())
        };

        Assert.DoesNotThrowAsync(async () =>
        {
            var scores = await _engine.ScoreSegmentsAsync(Guid.NewGuid(), segments, "test goal");
            Assert.That(scores, Is.Not.Empty);
        });
    }

    [Test]
    public void DecompressAsync_WithNullSegment_ReturnsNull()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await _compressor.DecompressAsync(null!);
            Assert.That(result, Is.Null);
        });
    }
}