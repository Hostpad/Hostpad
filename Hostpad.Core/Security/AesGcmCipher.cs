using System.Security.Cryptography;

namespace Hostpad.Core.Security;

/// <summary>
/// AES-256-GCM helpers. GCM is authenticated: a wrong key, a truncated file or a
/// flipped byte fails loudly instead of yielding plausible garbage.
/// </summary>
public static class AesGcmCipher
{
    public const int KeySizeBytes = 32;

    public const int NonceSizeBytes = 12;

    public const int TagSizeBytes = 16;

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeySizeBytes);

    public static WrappedKey Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return new WrappedKey { Nonce = nonce, Ciphertext = ciphertext, Tag = tag };
    }

    /// <exception cref="CryptographicException">The key is wrong or the data was tampered with.</exception>
    public static byte[] Decrypt(ReadOnlySpan<byte> key, WrappedKey wrapped)
    {
        var plaintext = new byte[wrapped.Ciphertext.Length];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(wrapped.Nonce, wrapped.Ciphertext, wrapped.Tag, plaintext);

        return plaintext;
    }
}
