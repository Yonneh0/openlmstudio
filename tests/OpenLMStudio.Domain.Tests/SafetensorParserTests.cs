using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenLMStudio.Infrastructure.Services;

namespace OpenLMStudio.Domain.Tests;

/// <summary>
/// Tests for the Safetensors header parser to ensure it correctly extracts tensor metadata.
/// </summary>
[TestFixture]
public class SafetensorParserTests
{
    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "openlmstudio-test");

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        Directory.CreateDirectory(TestDirectory);
    }

    [Test]
    public async Task ParseHeaderAsync_ReturnsNullForNonExistentFile()
    {
        // Arrange & Act
        var parser = new SafetensorParser(NullLogger<SafetensorParser>.Instance);
        var result = await parser.ParseHeaderAsync(Path.Combine(TestDirectory, "nonexistent.safetensors"));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ValidateHeaderAsync_ReturnsFalseForInvalidFile()
    {
        // Arrange — create a file with insufficient size to be valid safetensors.
        var tempFile = Path.Combine(TestDirectory, "too_small.safetensors");
        File.WriteAllText(tempFile, "small");

        try
        {
            var parser = new SafetensorParser(NullLogger<SafetensorParser>.Instance);
            var result = await parser.ValidateHeaderAsync(tempFile);

            // Assert
            Assert.That(result, Is.False);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ParseHeaderAsync_ReturnsNullForTooSmallFile()
    {
        // Arrange — create a 4-byte file (less than 8 bytes minimum).
        var tempFile = Path.Combine(TestDirectory, "too_small.safetensors");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x12, 0x34, 0x56, 0x78 });

        try
        {
            var parser = new SafetensorParser(NullLogger<SafetensorParser>.Instance);
            var result = await parser.ParseHeaderAsync(tempFile);

            // Assert
            Assert.That(result, Is.Null);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void ComputeSha256HashAsync_ReturnsNullForNonExistentFile()
    {
        // Arrange & Act — non-existent file should return null.
        var result = SafetensorParser.ComputeSha256HashAsync(Path.Combine(TestDirectory, "nonexistent.safetensors"));

        // Assert
        Assert.That(result.Result, Is.Null);
    }

    [Test]
    public async Task ParseHeaderAsync_ReturnsNullForCorruptedHeaderSize()
    {
        // Arrange — create a file with valid header size but corrupt JSON.
        var tempFile = Path.Combine(TestDirectory, "corrupt_header.safetensors");
        using (var stream = new FileStream(tempFile, FileMode.Create))
        {
            // Write 8 bytes for header size (0x123456789ABCDEF0 — way too large)
            var headerSizeBytes = System.Buffers.Binary.BinaryPrimitives.WriteLittleEndian(new byte[8], 0x123456789ABCDEF0UL);
            await stream.WriteAsync(headerSizeBytes);
        }

        try
        {
            var parser = new SafetensorParser(NullLogger<SafetensorParser>.Instance);
            var result = await parser.ParseHeaderAsync(tempFile);

            // Assert — header size exceeds file length so should be rejected.
            Assert.That(result, Is.Null);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ComputeMd5HashAsync_ReturnsNullForNonExistentFile()
    {
        // Arrange & Act — non-existent file should return null.
        var result = SafetensorParser.ComputeMd5HashAsync(Path.Combine(TestDirectory, "nonexistent.safetensors"));

        // Assert
        Assert.That(result.Result, Is.Null);
    }
}