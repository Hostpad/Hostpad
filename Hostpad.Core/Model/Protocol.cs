namespace Hostpad.Core.Model;

/// <summary>
/// Connection kinds Hostpad can launch. The value is persisted by name, so
/// existing members must never be renumbered or renamed once released.
/// </summary>
public enum Protocol
{
    Ssh = 0,
    Sftp = 1,
    Ftp = 2,
    Rdp = 3,
    Vnc = 4,
}

public static class ProtocolDefaults
{
    /// <summary>Port assumed when a connection does not specify one.</summary>
    public static int DefaultPort(this Protocol protocol) => protocol switch
    {
        Protocol.Ssh => 22,
        Protocol.Sftp => 22,
        Protocol.Ftp => 21,
        Protocol.Rdp => 3389,
        Protocol.Vnc => 5900,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
    };

    /// <summary>True when the protocol can authenticate with a private key file.</summary>
    public static bool SupportsKeyFile(this Protocol protocol) =>
        protocol is Protocol.Ssh or Protocol.Sftp;

    /// <summary>True when the protocol can be reached through an SSH jump host.</summary>
    public static bool SupportsJumpHost(this Protocol protocol) =>
        protocol is Protocol.Ssh or Protocol.Sftp;
}
