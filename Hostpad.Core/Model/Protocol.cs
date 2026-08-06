namespace Hostpad.Core.Model;

/// <summary>
/// Connection kinds Hostpad can launch. The value is persisted by name, so
/// existing members must never be renumbered or renamed once released.
/// <para>
/// A connection stores one of these as its <em>default</em>, used by the Connect
/// button and by double-click. It is not a restriction: the right-click menu can
/// launch the same host with any of them.
/// </para>
/// </summary>
public enum Protocol
{
    Ssh = 0,
    Sftp = 1,
    Ftp = 2,
    Rdp = 3,
    Vnc = 4,
    Scp = 5,
}

public static class ProtocolDefaults
{
    /// <summary>Order used by the Type dropdown and by the right-click connect menu.</summary>
    public static IReadOnlyList<Protocol> InMenuOrder { get; } =
    [
        Protocol.Ssh,
        Protocol.Rdp,
        Protocol.Vnc,
        Protocol.Sftp,
        Protocol.Scp,
        Protocol.Ftp,
    ];

    /// <summary>Port assumed when a connection does not specify one.</summary>
    public static int DefaultPort(this Protocol protocol) => protocol switch
    {
        Protocol.Ssh => 22,
        Protocol.Sftp => 22,
        Protocol.Scp => 22,
        Protocol.Ftp => 21,
        Protocol.Rdp => 3389,
        Protocol.Vnc => 5900,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
    };

    /// <summary>
    /// Label shown in the Type dropdown and the connect menu. Named after the
    /// tool rather than the protocol, because that is how users think about it.
    /// </summary>
    public static string DisplayName(this Protocol protocol) => protocol switch
    {
        Protocol.Ssh => "PuTTY",
        Protocol.Rdp => "Remote Desktop",
        Protocol.Vnc => "VNC",
        Protocol.Sftp => "WinSCP (SFTP)",
        Protocol.Scp => "WinSCP (SCP)",
        Protocol.Ftp => "WinSCP (FTP)",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
    };

    /// <summary>The three protocols WinSCP handles, which share one set of settings.</summary>
    public static bool UsesFileTransferTool(this Protocol protocol) =>
        protocol is Protocol.Sftp or Protocol.Scp or Protocol.Ftp;

    /// <summary>True when the protocol can authenticate with a private key file.</summary>
    public static bool SupportsKeyFile(this Protocol protocol) =>
        protocol is Protocol.Ssh or Protocol.Sftp or Protocol.Scp;

    /// <summary>True when the protocol can be reached through an SSH jump host.</summary>
    public static bool SupportsJumpHost(this Protocol protocol) =>
        protocol is Protocol.Ssh or Protocol.Sftp or Protocol.Scp;
}
