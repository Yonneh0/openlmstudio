global using System;
global using System.IO;
global using System.Threading.Tasks;

using NUnit.Framework;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// Tests for GGUF parser magic number validation and error handling.
/// </summary>
[TestFixture]
public class GgufParserTests
{
    private readonly DirectoryInfo _testOutputDir = new(Path.Combine(AppContext.BaseDirectory, "test-artifacts/gguf"));

    [SetUp]
    public void Setup()
    {
        _testOutputDir.Create();
    }

    [TearDown]
    public void TearDown()
    {
        if (_testOutputDir.Exists)
        {
            foreach (var file in _testOutputDir.EnumerateFiles())
                file.Delete();
            _testOutputDir.Delete(true);
        }
    }

    [Test]
    public void ParseAsync_WithCorruptedMagicNumber_ReturnsFalse()
    {
        var corruptFile = new FileInfo(Path.Combine(_testOutputDir.FullName, "corrupted.gguf"));
        File.WriteAllBytes(corruptFile.FullName, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01 });

        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await new GgufParser().ParseAsync(corruptFile.FullName);
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void ParseAsync_WithShortFile_ReturnsFalse()
    {
        var shortFile = new FileInfo(Path.Combine(_testOutputDir.FullName, "short.gguf"));
        File.WriteAllBytes(shortFile.FullName, new byte[] { 0x46, 0x46, 0x4D });

        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await new GgufParser().ParseAsync(shortFile.FullName);
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void ParseAsync_WithNonExistentFile_ReturnsFalse()
    {
        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await new GgufParser().ParseAsync("nonexistent.gguf");
            Assert.That(result, Is.Null);
        });
    }

    [Test]
    public void ParseAsync_WithEmptyFile_ReturnsFalse()
    {
        var emptyFile = new FileInfo(Path.Combine(_testOutputDir.FullName, "empty.gguf"));
        File.WriteAllBytes(emptyFile.FullName, Array.Empty<byte>());

        Assert.DoesNotThrowAsync(async () =>
        {
            var result = await new GgufParser().ParseAsync(emptyFile.FullName);
            Assert.That(result, Is.Null);
        });
    }
}