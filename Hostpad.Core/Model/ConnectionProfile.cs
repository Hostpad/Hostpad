namespace Hostpad.Core.Model;

/// <summary>
/// A reusable bundle of launcher settings, so "PuTTY with this key and this
/// post-login command" is configured once instead of per connection.
/// </summary>
public sealed class ConnectionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>Profiles are protocol-specific; a connection may only reference a matching one.</summary>
    public required Protocol Protocol { get; set; }

    /// <summary>
    /// Launcher settings keyed by <see cref="ProfileKeys"/>. Kept as strings
    /// rather than typed properties because the set differs per tool and grows
    /// as tools gain switches — a schema change should not be needed to expose one.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionProfile Clone() => new()
    {
        Name = Name,
        Protocol = Protocol,
        Settings = new Dictionary<string, string>(Settings, StringComparer.OrdinalIgnoreCase),
    };

    public override string ToString() => $"{Name} ({Protocol})";
}

/// <summary>
/// Known keys for <see cref="ConnectionProfile.Settings"/> and
/// <see cref="Connection.Overrides"/>. Namespaced by tool so keys never collide.
/// Unknown keys are preserved on round-trip rather than dropped.
/// </summary>
public static class ProfileKeys
{
    // SSH / SFTP
    public const string SshExtraArgs = "ssh.extraArgs";
    public const string SshPostLoginCommand = "ssh.postLoginCommand";
    public const string SshAgentForwarding = "ssh.agentForwarding";
    public const string SshCompression = "ssh.compression";

    // FTP / SFTP transfer client
    public const string FtpPassiveMode = "ftp.passiveMode";
    public const string FtpRemotePath = "ftp.remotePath";

    // RDP
    public const string RdpShareDrives = "rdp.shareDrives";
    public const string RdpAdminSession = "rdp.adminSession";
    public const string RdpFullScreen = "rdp.fullScreen";
    public const string RdpMultiMon = "rdp.multiMon";
    public const string RdpResolution = "rdp.resolution";

    // VNC
    public const string VncViewOnly = "vnc.viewOnly";
    public const string VncFullScreen = "vnc.fullScreen";
    public const string VncQuality = "vnc.quality";
}
