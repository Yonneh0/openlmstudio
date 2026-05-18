using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Cryptographic utility for OpenLMStudio. Provides AES-256 encryption for data at rest.
/// Uses PBKDF2 key derivation (HMAC-SHA256) with 100,000 iterations per platform keychain standards.
/// </summary>
public static class Crypto
{
    private const int KeySizeBits = 256;
    private const int IvSizeBytes = 16; // AES block size for IV
    private const int SaltSizeBytes = 32;
    private const int Iterations = 100_000; // PBKDF2 iterations

    /// <summary>
    /// Generates a random key for use with OpenLMStudio crypto operations.
    /// The caller is responsible for securely storing this key (e.g., in OS keychain).
    /// </summary>
    public static byte[] GenerateKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var key = new byte[KeySizeBits / 8];
        rng.GetBytes(key);
        return key;
    }

    /// <summary>
    /// Encrypts data using AES-256-CBC with PBKDF2-derived key.
    /// Format: [Salt (32 bytes)][IV (16 bytes)][Encrypted Data]
    /// </summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        if (plaintext == null || plaintext.Length == 0)
            return Array.Empty<byte>();

        using var aes = Aes.Create();
        aes.KeySize = KeySizeBits;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Derive key and IV from password + salt via PBKDF2
        byte[] salt, iv;
        using (var rng = RandomNumberGenerator.Create())
        {
            salt = new byte[SaltSizeBytes];
            iv = new byte[IvSizeBytes];
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        var derivedKey = new Rfc2898DeriveBytes(key, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(KeySizeBits / 8);

        aes.Key = derivedKey;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        byte[] encryptedContent;

        using (var ms = new MemoryStream())
        {
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(plaintext, 0, plaintext.Length);
                cs.FlushFinalBlock();
            }
            encryptedContent = ms.ToArray();
        }

        // Combine: salt || iv || encrypted_content
        using var resultMs = new MemoryStream(salt.Length + iv.Length + encryptedContent.Length);
        resultMs.Write(salt, 0, salt.Length);
        resultMs.Write(iv, 0, iv.Length);
        resultMs.Write(encryptedContent, 0, encryptedContent.Length);

        return resultMs.ToArray();
    }

    /// <summary>
    /// Decrypts data using AES-256-CBC with PBKDF2-derived key.
    /// Expects format: [Salt (32 bytes)][IV (16 bytes)][Encrypted Data]
    /// </summary>
    public static byte[] Decrypt(byte[] ciphertext, byte[] key)
    {
        if (ciphertext == null || ciphertext.Length < SaltSizeBytes + IvSizeBytes)
            throw new CryptographicException("Ciphertext is too short — missing salt or IV.");

        // Extract salt and IV from ciphertext header
        var salt = new byte[SaltSizeBytes];
        var iv = new byte[IvSizeBytes];
        Array.Copy(ciphertext, 0, salt, 0, SaltSizeBytes);
        Array.Copy(ciphertext, SaltSizeBytes, iv, 0, IvSizeBytes);

        // Extract encrypted content
        var encryptedContent = new byte[ciphertext.Length - SaltSizeBytes - IvSizeBytes];
        Array.Copy(ciphertext, SaltSizeBytes + IvSizeBytes, encryptedContent, 0, encryptedContent.Length);

        using var aes = Aes.Create();
        aes.KeySize = KeySizeBits;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Derive key from password + salt via PBKDF2 (same as encryption)
        using var kdfCsp = new Rfc2898DeriveBytes(key, salt, Iterations, HashAlgorithmName.SHA256);
        aes.Key = kdfCsp.GetBytes(KeySizeBits / 8);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] decryptedContent;

        try
        {
            using (var ms = new MemoryStream(encryptedContent))
            {
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    decryptedContent = new byte[encryptedContent.Length];
                    var bytesRead = cs.Read(decryptedContent, 0, encryptedContent.Length);
                    Array.Resize(ref decryptedContent, bytesRead);
                }
            }

            return decryptedContent;
        }
        catch (CryptographicException)
        {
            // Wrong password — throw a clear exception
            throw new CryptographicException("Decryption failed — incorrect key or corrupted data.");
        }
    }

    /// <summary>
    /// Derives an AES key from the given password and salt using PBKDF2.
    /// This is a helper used internally; callers should prefer Encrypt/Decrypt which handle derivation automatically.
    /// </summary>
    public static byte[] DeriveKey(byte[] password, byte[] salt)
    {
        using var kdfCsp = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return kdfCsp.GetBytes(KeySizeBits / 8);
    }

    /// <summary>
    /// Computes SHA-256 hash of data for integrity verification.
    /// </summary>
    public static byte[] ComputeSha256Hash(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }

    /// <summary>
    /// Compares two byte arrays for equality in constant time (prevents timing attacks).
    /// </summary>
    public static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }
}