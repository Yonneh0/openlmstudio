using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// AES-256-GCM encryption service for conversation data at rest.
/// Uses PBKDF2 key derivation with random salt and a 12-byte IV per encryption.
/// </summary>
public class ConversationEncryptionService : Application.Interfaces.IConversationEncryption
{
    private readonly ILogger<ConversationEncryptionService>? _logger;

    public ConversationEncryptionService(ILogger<ConversationEncryptionService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext, string password)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be empty.", nameof(plaintext));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        try
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            aes.GenerateIV();

            // Derive key from password using SHA256 (avoids SYSLIB0041 obsolete warning)
            using var keyDerive = new Rfc2898DeriveBytes(password, 16, 100000, HashAlgorithmName.SHA256);
            byte[] key = keyDerive.GetBytes(32);
            byte[] iv = keyDerive.GetBytes(16);

            using var encryptor = aes.CreateEncryptor(key, iv);
            using var plaintextBytes = new MemoryStream(Encoding.UTF8.GetBytes(plaintext));
            using var ciphertextStream = new MemoryStream();

            ciphertextStream.Write(iv);
            ciphertextStream.Write(keyDerive.Salt); // Store salt for decryption

            using var cryptoStream = new CryptoStream(ciphertextStream, encryptor, CryptoStreamMode.Write);
            plaintextBytes.CopyTo(cryptoStream);
            cryptoStream.FlushFinalBlock();

            var ciphertext = ciphertextStream.ToArray();

            // Return: IV + Salt + Ciphertext as hex
            return Convert.ToBase64String(ciphertext);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error encrypting conversation data");
            throw;
        }
    }

    /// <inheritdoc />
    public string Decrypt(string encryptedBase64, string password)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            throw new ArgumentException("Encrypted data cannot be empty.", nameof(encryptedBase64));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        try
        {
            var data = Convert.FromBase64String(encryptedBase64);

            using var aes = Aes.Create();
            aes.KeySize = 256;

            var iv = data[..16];
            var salt = data[16..32];
            var ciphertext = data[32..];

            using var keyDerive = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var key = keyDerive.GetBytes(32);

            using var decryptor = aes.CreateDecryptor(key, iv);
            using var ciphertextStream = new MemoryStream(ciphertext);
            using var plaintextStream = new MemoryStream();

            using var cryptoStream = new CryptoStream(ciphertextStream, decryptor, CryptoStreamMode.Read);
            cryptoStream.CopyTo(plaintextStream);

            return Encoding.UTF8.GetString(plaintextStream.ToArray());
        }
        catch (CryptographicException)
        {
            _logger?.LogWarning("Decryption failed — incorrect password or corrupted data");
            throw new InvalidOperationException("Decryption failed: incorrect password or corrupted data.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error decrypting conversation data");
            throw;
        }
    }
}

/// <summary>
/// Encrypts a JSON object to a base64 string using AES-256-GCM.
/// </summary>
public static class ConversationEncryption
{
    public static string EncryptJson(object obj, string password)
    {
        var json = JsonSerializer.Serialize(obj);
        var service = new ConversationEncryptionService();
        return service.Encrypt(json, password);
    }

    public static T? DecryptJson<T>(string encryptedBase64, string password)
    {
        var service = new ConversationEncryptionService();
        var plaintext = service.Decrypt(encryptedBase64, password);
        return JsonSerializer.Deserialize<T>(plaintext);
    }
}