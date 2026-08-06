namespace Hostpad.Core.Security;

/// <summary>
/// An AES-GCM ciphertext together with the nonce and tag needed to open it.
/// Used both for the document payload and for the wrapped data key.
/// </summary>
public sealed class WrappedKey
{
    public required byte[] Nonce { get; init; }

    public required byte[] Ciphertext { get; init; }

    public required byte[] Tag { get; init; }
}
