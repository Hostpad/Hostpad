using System.Globalization;
using System.Text;
using Hostpad.Core.Model;
using Hostpad.Core.Security;

namespace Hostpad.Core.Launching;

/// <summary>
/// Builds a VNC session through a generated .vnc file, which is how viewers
/// accept a password without showing it on the command line.
/// </summary>
public sealed class VncLauncher : ILauncher
{
    public bool Handles(Protocol protocol) => protocol is Protocol.Vnc;

    public LaunchPlan CreatePlan(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = request.Settings.Vnc;
        var credential = request.Connection.Credential;
        var file = new StringBuilder();

        file.AppendLine("[Connection]");
        file.AppendLine(CultureInfo.InvariantCulture, $"Host={request.Connection.Host}");
        file.AppendLine(CultureInfo.InvariantCulture, $"Port={request.Port}");

        if (credential.HasPassword)
        {
            file.AppendLine(CultureInfo.InvariantCulture, $"Password={VncPasswordCipher.Encrypt(credential.Password!)}");
        }

        file.AppendLine("[Options]");
        file.AppendLine(CultureInfo.InvariantCulture, $"FullScreen={(settings.FullScreen ? 1 : 0)}");
        file.AppendLine(CultureInfo.InvariantCulture, $"ViewOnly={(settings.ViewOnly ? 1 : 0)}");

        var path = TemporaryFiles.Write("hostpad-vnc", ".vnc", file.ToString(), settings.OutputPath);

        return new LaunchPlan
        {
            FileName = settings.Path,
            Arguments = [path],
            TemporaryFile = string.IsNullOrWhiteSpace(settings.OutputPath) ? path : null,
        };
    }
}
