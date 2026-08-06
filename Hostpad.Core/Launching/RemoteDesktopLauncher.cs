using System.Globalization;
using System.Text;
using Hostpad.Core.Model;
using Hostpad.Core.Security;

namespace Hostpad.Core.Launching;

/// <summary>
/// Builds a Remote Desktop session. mstsc takes almost nothing on the command
/// line, so the settings go into a generated .rdp file instead.
/// </summary>
public sealed class RemoteDesktopLauncher : ILauncher
{
    public bool Handles(Protocol protocol) => protocol is Protocol.Rdp;

    public LaunchPlan CreatePlan(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = request.Settings.RemoteDesktop;
        var credential = request.Connection.Credential;
        var file = new StringBuilder();

        file.AppendLine(CultureInfo.InvariantCulture, $"full address:s:{request.Connection.Host}:{request.Port}");
        file.AppendLine("prompt for credentials:i:0");

        if (!string.IsNullOrEmpty(credential.Username))
        {
            file.AppendLine(CultureInfo.InvariantCulture, $"username:s:{credential.Username}");
        }

        if (credential.HasPassword && OperatingSystem.IsWindows())
        {
            file.AppendLine(CultureInfo.InvariantCulture, $"password 51:b:{RdpPasswordProtector.Protect(credential.Password!)}");
        }

        if (ParseScreenSize(settings.ScreenSize) is { } size)
        {
            file.AppendLine("screen mode id:i:1");
            file.AppendLine(CultureInfo.InvariantCulture, $"desktopwidth:i:{size.Width}");
            file.AppendLine(CultureInfo.InvariantCulture, $"desktopheight:i:{size.Height}");
        }
        else
        {
            file.AppendLine("screen mode id:i:2");
        }

        file.AppendLine(CultureInfo.InvariantCulture, $"redirectdrives:i:{(settings.MountLocalDrives ? 1 : 0)}");
        file.AppendLine(CultureInfo.InvariantCulture, $"use multimon:i:{(settings.MultipleMonitors ? 1 : 0)}");

        var path = TemporaryFiles.Write("hostpad-rdp", ".rdp", file.ToString(), settings.OutputPath);
        var arguments = new List<string> { path };

        if (settings.AdminSession)
        {
            arguments.Add("/admin");
        }

        if (settings.MultipleMonitors)
        {
            arguments.Add("/multimon");
        }

        return new LaunchPlan
        {
            FileName = settings.Path,
            Arguments = arguments,

            // Kept when the user asked for the files to be saved somewhere;
            // otherwise it is scratch and gets cleaned up after launch.
            TemporaryFile = string.IsNullOrWhiteSpace(settings.OutputPath) ? path : null,
        };
    }

    /// <summary>Reads "1920x1080". Returns null for anything else, including blank.</summary>
    private static (int Width, int Height)? ParseScreenSize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split('x', 'X', '*');

        return parts.Length == 2 &&
               int.TryParse(parts[0].Trim(), out var width) &&
               int.TryParse(parts[1].Trim(), out var height) &&
               width > 0 && height > 0
            ? (width, height)
            : null;
    }
}
