using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;

namespace NxtHotspot.Infrastructure.Security;

public sealed class SecurityOptions
{
    /// <summary>Base64 of exactly 32 random bytes. Supply via environment/user-secrets, never source control.</summary>
    public string EncryptionKey { get; set; } = "";
}

/// <summary>
/// AES-256-GCM. Output format (base64): version(1) | nonce(12) | tag(16) | ciphertext.
/// The version byte allows key rotation to be added later.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<SecurityOptions> options)
    {
        var raw = options.Value.EncryptionKey;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Security:EncryptionKey is not configured (base64 of 32 random bytes).");
        try { _key = Convert.FromBase64String(raw); }
        catch (FormatException) { throw new InvalidOperationException("Security:EncryptionKey must be valid base64."); }
        if (_key.Length != 32)
            throw new InvalidOperationException("Security:EncryptionKey must decode to exactly 32 bytes.");
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, data, cipher, tag);

        var output = new byte[1 + NonceSize + TagSize + cipher.Length];
        output[0] = Version;
        Buffer.BlockCopy(nonce, 0, output, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, output, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, 1 + NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedValue)
    {
        var input = Convert.FromBase64String(protectedValue);
        if (input.Length < 1 + NonceSize + TagSize || input[0] != Version)
            throw new CryptographicException("Unrecognized protected value.");

        var nonce = input.AsSpan(1, NonceSize);
        var tag = input.AsSpan(1 + NonceSize, TagSize);
        var cipher = input.AsSpan(1 + NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain); // throws if tampered
        return Encoding.UTF8.GetString(plain);
    }
}
