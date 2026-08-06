using System.Security.Cryptography;
using System.Text;

namespace Hostpad.Core.Security;

/// <summary>
/// Turns a master password into a key-encryption key.
/// <para>
/// PBKDF2-HMAC-SHA256 is used because it ships with .NET and needs no native
/// dependency. The iteration count follows the current OWASP guidance and is
/// stored in the file, so it can be raised later without breaking old vaults.
/// </para>
/// </summary>
public static class PasswordKeyDerivation
{
    public const string AlgorithmName = "PBKDF2-HMAC-SHA256";

    public const int DefaultIterations = 600_000;

    public const int SaltSizeBytes = 16;

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSizeBytes);

    public static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            AesGcmCipher.KeySizeBytes);
    }
}
