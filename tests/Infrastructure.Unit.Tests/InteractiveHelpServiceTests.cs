// Brought to you by Carls' Jr.
using Microsoft.Extensions.Logging.Abstractions;
using OpenLMStudio.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Unit.Tests;

public class InteractiveHelpServiceTests : IDisposable
{
    private readonly string _testDir;

    public InteractiveHelpServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"help-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task GetHelpAsync_ReturnsContent_WhenTopicExists()
    {
        var appData = new AppDataDirectoryResolver();
        var service = new InteractiveHelpService(NullLogger<InteractiveHelpService>.Instance, appData);

        var result = await service.GetHelpAsync("troubleshooting");
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetHelpAsync_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var appData = new AppDataDirectoryResolver();
        var service = new InteractiveHelpService(NullLogger<InteractiveHelpService>.Instance, appData);

        var result = await service.GetHelpAsync("nonexistent-topic-xyz");
        Assert.Contains("No help found", result);
    }

    [Fact]
    public async Task AddTopicAsync_CreatesNewTopic()
    {
        var appData = new AppDataDirectoryResolver();
        var service = new InteractiveHelpService(NullLogger<InteractiveHelpService>.Instance, appData);

        var added = await service.AddTopicAsync("Test Topic", "# Test Content");
        Assert.True(added);

        var result = await service.GetHelpAsync("Test Topic");
        Assert.Equal("# Test Content", result);
    }

    [Fact]
    public async Task SearchHelpAsync_ReturnsMatchingTopics()
    {
        var appData = new AppDataDirectoryResolver();
        var service = new InteractiveHelpService(NullLogger<InteractiveHelpService>.Instance, appData);

        var results = await service.SearchHelpAsync("API");
        Assert.NotNull(results);
    }
}