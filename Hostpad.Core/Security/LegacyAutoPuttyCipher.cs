using System.Security.Cryptography;
using System.Text;

namespace Hostpad.Core.Security;

/// <summary>
/// Reads the field encryption AutoPuTTY used in autoputty.xml, so existing
/// lists can be imported.
/// <para>
/// Triple DES in ECB mode with a key derived from MD5 of a passphrase. When no
/// master password was set, that passphrase is a constant published in
/// AutoPuTTY's own configuration file, which means those files are readable by
/// anyone and should be treated as plaintext. This exists to read them, never
/// to write them.
/// </para>
/// </summary>
public static class LegacyAutoPuttyCipher
{
    /// <summary>AutoPuTTY's built-in key, used whenever the user set no master password.</summary>
    public const string DefaultPassphrase = "12ùMkldQ%kS2A";

    /// <summary>Returns null when the input cannot be decrypted with this passphrase.</summary>
    public static string? TryDecrypt(string? value, string passphrase)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            var cipherText = Convert.FromBase64String(value);

            using var tripleDes = TripleDES.Create();
            tripleDes.Key = MD5.HashData(Encoding.UTF8.GetBytes(passphrase));
            tripleDes.Mode = CipherMode.ECB;
            tripleDes.Padding = PaddingMode.PKCS7;

            using var decryptor = tripleDes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Wrong passphrase, or a field that was never encrypted.
            return null;
        }
    }
}
