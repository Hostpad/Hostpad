using System.Security.Cryptography;
using System.Text;

namespace Hostpad.Core.Security;

/// <summary>
/// Obfuscates a password the way VNC viewers expect it in a .vnc file.
/// <para>
/// This is not security. The VNC protocol has used one published DES key
/// since the 1990s, so anyone can reverse it; the format simply requires it.
/// Confidentiality comes from Hostpad's own vault, and the generated .vnc file
/// is deleted as soon as the viewer has read it.
/// </para>
/// </summary>
public static class VncPasswordCipher
{
    /// <summary>The fixed key every VNC implementation uses, published for decades.</summary>
    private static readonly byte[] VncKey = [23, 82, 107, 6, 35, 78, 88, 7];

    private const int PasswordLength = 8;

    public static string Encrypt(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        // VNC truncates to eight characters and zero-pads shorter ones. The
        // protocol is ASCII, so anything outside it cannot round-trip anyway.
        var block = new byte[PasswordLength];
        var bytes = Encoding.ASCII.GetBytes(password);
        Array.Copy(bytes, block, Math.Min(bytes.Length, PasswordLength));

        using var des = DES.Create();
        des.Key = MirrorBits(VncKey);
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;

        using var encryptor = des.CreateEncryptor();
        return Convert.ToHexString(encryptor.TransformFinalBlock(block, 0, PasswordLength)).ToLowerInvariant();
    }

    /// <summary>
    /// VNC feeds the key bits least-significant first, so every byte is
    /// reversed before use. Without this the output is wrong but still
    /// plausible-looking, which is why it is worth stating plainly.
    /// </summary>
    private static byte[] MirrorBits(byte[] key)
    {
        var mirrored = new byte[key.Length];

        for (var i = 0; i < key.Length; i++)
        {
            var value = key[i];
            byte result = 0;

            for (var bit = 0; bit < 8; bit++)
            {
                result <<= 1;
                result |= (byte)(value & 1);
                value >>= 1;
            }

            mirrored[i] = result;
        }

        return mirrored;
    }
}
