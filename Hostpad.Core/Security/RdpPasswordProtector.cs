using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Hostpad.Core.Security;

/// <summary>
/// Produces the password blob mstsc expects in a .rdp file.
/// <para>
/// Remote Desktop stores passwords as a DPAPI blob of the UTF-16 password,
/// written as uppercase hex after "password 51:b:". The blob is tied to the
/// Windows account, so a generated .rdp file only works for the user who
/// created it — which is exactly the property wanted here.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public static class RdpPasswordProtector
{
    public static string Protect(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var blob = ProtectedData.Protect(
            Encoding.Unicode.GetBytes(password),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);

        return Convert.ToHexString(blob);
    }
}
