namespace Hostpad.Core.Security;

/// <summary>
/// The on-disk structure of a vault file.
/// <para>
/// The document is encrypted once with a random data key; that data key is then
/// wrapped by DPAPI, by the master password, or by both. Changing the password
/// therefore rewraps a 32-byte key instead of re-encrypting the whole document,
/// and a vault can be protected by the Windows account alone, by a password
/// alone, or by both at once.
/// </para>
/// </summary>
public sealed class VaultEnvelope
{
    public const string ExpectedMagic = "Hostpad";

    public const int CurrentFormatVersion = 1;

    public string Magic { get; set; } = ExpectedMagic;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>Data key wrapped by DPAPI. Null when the vault is password-only.</summary>
    public byte[]? DpapiWrappedKey { get; set; }

    /// <summary>Parameters used to derive the password key. Null when there is no master password.</summary>
    public KdfParameters? Kdf { get; set; }

    /// <summary>Data key wrapped by the password-derived key. Null when there is no master password.</summary>
    public WrappedKey? PasswordWrappedKey { get; set; }

    /// <summary>The encrypted document.</summary>
    public required WrappedKey Payload { get; set; }

    public bool HasPassword => Kdf is not null && PasswordWrappedKey is not null;

    public bool HasDpapi => DpapiWrappedKey is not null;
}

public sealed class KdfParameters
{
    public string Algorithm { get; set; } = PasswordKeyDerivation.AlgorithmName;

    public int Iterations { get; set; } = PasswordKeyDerivation.DefaultIterations;

    public required byte[] Salt { get; set; }
}
