using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenLMStudio.Domain.Models;

/// <summary>
/// AES-256 encryption service for conversation data at rest.
/// Keys are stored in platform-specific keychain (Windows: DPAPI, macOS: Keychain, Linux: keyring).
/// </summary>
public static class ConversationEncryption
{
    private const int KeySizeBits = 256;
    private const int IvsSizeBytes = 16;
    private const int SaltSizeBytes = 32;
    private const int Iterations = 100_000;

    /// <summary>
    /// Encrypts the plaintext string to a base64-encoded ciphertext with HMAC integrity check.
    /// </summary>
    public static string Encrypt(string plaintext, string password)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        using var aes = Aes.Create();
        aes.KeySize = KeySizeBits;
        aes.GenerateIV();
        aes.GenerateKey();

        var derivedKey = DeriveKey(password, aes.Key, aes.IV);
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        var hmac = ComputeHmac(derivedKey, encrypted);

        using var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(hmac.Length), 0, 4);
        ms.Write(hmac, 0, hmac.Length);
        ms.Write(iv, 0, iv.Length);
        ms.Write(encrypted, 0, encrypted.Length);

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts a base64-encoded ciphertext string using the provided password.
    /// </summary>
    public static string Decrypt(string ciphertext, string password)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        var bytes = Convert.FromBase64String(ciphertext);

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        var hmacLength = reader.ReadInt32();
        var hmac = reader.ReadBytes(hmacLength);
        var iv = reader.ReadBytes(IvsSizeBytes);
        var encrypted = reader.ReadBytes(bytes.Length - 4 - hmacLength - IvsSizeBytes);

        var derivedKey = DeriveKey(password, encrypted, iv);
        var expectedHmac = ComputeHmac(derivedKey, encrypted);

        if (!ConstantTimeCompare(hmac, expectedHmac))
            throw new CryptographicException("Decryption failed: integrity check failed (tampered ciphertext)");

        using var aes = Aes.Create();
        using var decryptor = aes.CreateDecryptor(encrypted, iv);
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    private static byte[] DeriveKey(string password, byte[] key, byte[] iv)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, key, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySizeBits / 8);
    }

    private static byte[] ComputeHmac(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private static bool ConstantTimeCompare(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}