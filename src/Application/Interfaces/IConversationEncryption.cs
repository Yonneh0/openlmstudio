using System;

namespace OpenLMStudio.Application.Interfaces;

/// <summary>
/// Contract for AES-256-GCM conversation data encryption at rest.
/// </summary>
public interface IConversationEncryption
{
    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with PBKDF2 key derivation.
    /// </summary>
    /// <param name="plaintext">The text to encrypt.</param>
    /// <param name="password">The password for key derivation.</param>
    /// <returns>Base64-encoded ciphertext (IV + Salt + Ciphertext).</returns>
    string Encrypt(string plaintext, string password);

    /// <summary>
    /// Decrypts base64-encoded ciphertext using the provided password.
    /// </summary>
    /// <param name="encryptedBase64">The encrypted data to decrypt.</param>
    /// <param name="password">The password used for key derivation.</param>
    /// <returns>The decrypted plaintext.</returns>
    string Decrypt(string encryptedBase64, string password);
}