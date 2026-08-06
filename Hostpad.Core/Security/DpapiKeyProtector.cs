using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Hostpad.Core.Security;

/// <summary>
/// Binds the data key to the current Windows user account through DPAPI, so a
/// vault copied to another machine or opened by another user cannot be read
/// without the master password.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DpapiKeyProtector
{
    /// <summary>Mixed into the DPAPI blob so a key from another application cannot be substituted.</summary>
    private static readonly byte[] Entropy = "Hostpad.Vault.v1"u8.ToArray();

    public static byte[] Protect(byte[] key) =>
        ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser);

    /// <exception cref="CryptographicException">Wrong Windows user, or a corrupted blob.</exception>
    public static byte[] Unprotect(byte[] protectedKey) =>
        ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser);
}
