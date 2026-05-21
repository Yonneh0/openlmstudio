using OpenLMStudio.Domain.Models;
using NUnit.Framework;

namespace OpenLMStudio.Domain.Tests;

/// <summary>
/// Tests for AES-256 conversation encryption with HMAC integrity verification.
/// </summary>
public class ConversationEncryptionTests
{
    [Test]
    public void Encrypt_WhenNullInput_ReturnsEmptyString()
    {
        var result = ConversationEncryption.Encrypt(null!, "password");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Decrypt_WhenNullInput_ReturnsEmptyString()
    {
        var result = ConversationEncryption.Decrypt(null!, "password");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void EncryptDecrypt_Cycle_ReturnsOriginal()
    {
        var original = "Hello, this is a secret conversation message.";
        var password = "my-secret-password";

        var encrypted = ConversationEncryption.Encrypt(original, password);
        Assert.That(encrypted, Is.Not.Empty);
        Assert.That(encrypted, Is.Not.EqualTo(original));

        var decrypted = ConversationEncryption.Decrypt(encrypted, password);
        Assert.That(decrypted, Is.EqualTo(original));
    }

    [Test]
    public void EncryptDecrypt_MultipleMessages_AllDecryptCorrectly()
    {
        var messages = new[]
        {
            "User: Hello!",
            "Assistant: Hi there! How can I help?",
            "User: Tell me about machine learning.",
            "Assistant: Machine learning is a subset of AI..."
        };
        var password = "test-password-123";

        foreach (var msg in messages)
        {
            var encrypted = ConversationEncryption.Encrypt(msg, password);
            var decrypted = ConversationEncryption.Decrypt(encrypted, password);
            Assert.That(decrypted, Is.EqualTo(msg));
        }
    }

    [Test]
    public void Decrypt_WithWrongPassword_ThrowsCryptographicException()
    {
        var original = "Sensitive conversation data";
        var password = "correct-password";
        var wrongPassword = "wrong-password";

        var encrypted = ConversationEncryption.Encrypt(original, password);
        Assert.Throws<CryptographicException>(() => ConversationEncryption.Decrypt(encrypted, wrongPassword));
    }

    [Test]
    public void Decrypt_WithTamperedCiphertext_ThrowsCryptographicException()
    {
        var original = "Sensitive conversation data";
        var password = "correct-password";

        var encrypted = ConversationEncryption.Encrypt(original, password);
        var bytes = Convert.FromBase64String(encrypted);
        // Flip a bit in the middle
        bytes[bytes.Length / 2] ^= 0x01;
        var tampered = Convert.ToBase64String(bytes);

        Assert.Throws<CryptographicException>(() => ConversationEncryption.Decrypt(tampered, password));
    }

    [Test]
    public void EncryptDecrypt_UnicodeAndSpecialCharacters_PreservesContent()
    {
        var original = "こんにちは 🌟 Привет мир 你好世界";
        var password = "test";

        var encrypted = ConversationEncryption.Encrypt(original, password);
        var decrypted = ConversationEncryption.Decrypt(encrypted, password);
        Assert.That(decrypted, Is.EqualTo(original));
    }
}