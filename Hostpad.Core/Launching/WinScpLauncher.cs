using Hostpad.Core.Model;

namespace Hostpad.Core.Launching;

/// <summary>Builds a WinSCP command line for the SFTP, SCP and FTP session types.</summary>
public sealed class WinScpLauncher : ILauncher
{
    public bool Handles(Protocol protocol) => protocol.UsesFileTransferTool();

    public LaunchPlan CreatePlan(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = request.Settings.WinScp;
        var credential = request.Connection.Credential;

        var scheme = request.Protocol switch
        {
            Protocol.Sftp => "sftp",
            Protocol.Scp => "scp",
            Protocol.Ftp => "ftp",
            _ => throw new LaunchException($"WinSCP does not handle {request.Protocol}."),
        };

        var authority = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(credential.Username))
        {
            authority.Append(Uri.EscapeDataString(credential.Username));

            if (credential.HasPassword)
            {
                authority.Append(':').Append(Uri.EscapeDataString(credential.Password!));
            }

            authority.Append('@');
        }

        authority.Append(request.Connection.Host).Append(':').Append(request.Port);

        var arguments = new List<string> { $"{scheme}://{authority}/" };

        if (settings.UseKeyFile && !string.IsNullOrWhiteSpace(settings.KeyFilePath))
        {
            arguments.Add($"/privatekey={settings.KeyFilePath}");
        }

        // Passive mode is an FTP concept; sending it for SFTP or SCP confuses WinSCP.
        if (request.Protocol is Protocol.Ftp)
        {
            arguments.Add($"/passive={(settings.PassiveMode ? "on" : "off")}");
        }

        return new LaunchPlan
        {
            FileName = settings.Path,
            Arguments = arguments,
        };
    }
}
