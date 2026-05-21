using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using OpenLMStudio.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Unit.Tests;

/// <summary>
/// Tests for AES-256-GCM encryption of conversation data at rest.
/// </summary>
public class ConversationEncryptionTests
{
    private readonly ConversationEncryptionService _service = new();

    [Fact]
    public void EncryptDecrypt_Roundtrip_ProducesOriginalText()
    {
        var original = "Hello, this is a test conversation with special characters: ñ émojis 🎉";
        var password = "super-secret";

        var encrypted = _service.Encrypt(original, password);
        var decrypted = _service.Decrypt(encrypted, password);

        Assert.NotEqual(original, encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_JsonRoundtrip_ProducesOriginalObject()
    {
        var json = @"{""messages"":[{""role"":""user"",""content"":""Hello""}]}";
        var password = "test-password";

        var encrypted = _service.Encrypt(json, password);
        var decrypted = _service.Decrypt(encrypted, password);

        Assert.Equal(json, decrypted);
    }

    [Fact]
    public void Decrypt_WrongPassword_ThrowsInvalidOperationException()
    {
        var encrypted = _service.Encrypt("sensitive data", "correct-password");

        Assert.Throws<InvalidOperationException>(() => _service.Decrypt(encrypted, "wrong-password"));
    }

    [Fact]
    public void Encrypt_Decrypt_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.Encrypt("", "password"));
    }

    [Fact]
    public void Encrypt_Decrypt_EmptyPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _service.Decrypt("data", ""));
    }

    [Fact]
    public void Encrypt_MultipleCalls_DifferentOutput_EachTime()
    {
        var original = "same message";
        var password = "password";

        var encrypted1 = _service.Encrypt(original, password);
        var encrypted2 = _service.Encrypt(original, password);

        // Different IV/salt should produce different ciphertext
        Assert.NotEqual(encrypted1, encrypted2);

        // Both should decrypt to the same value
        Assert.Equal(_service.Decrypt(encrypted1, password), _service.Decrypt(encrypted2, password));
    }

    [Fact]
    public void EncryptDecrypt_TamperedCiphertext_ThrowsInvalidOperationException()
    {
        var encrypted = _service.Encrypt("sensitive", "password");
        var bytes = Convert.FromBase64String(encrypted);

        // Flip a byte in the ciphertext portion (after IV + Salt)
        if (bytes.Length > 32)
            bytes[32] ^= 0xFF;

        var tampered = Convert.ToBase64String(bytes);

        Assert.Throws<InvalidOperationException>(() => _service.Decrypt(tampered, "password"));
    }
}