namespace Hostpad.Core.Model;

/// <summary>
/// An intermediate SSH host tunnelled through to reach the target.
/// <para>
/// AutoPuTTY encoded this inside the username field as
/// <c>proxyuser@proxyhost:proxyport#user</c>. Hostpad models it as real data;
/// the legacy syntax is only parsed by the importer.
/// </para>
/// </summary>
public sealed class JumpHost
{
    public required string Host { get; set; }

    public string? Username { get; set; }

    /// <summary>Null means the target protocol's default port.</summary>
    public int? Port { get; set; }

    public int EffectivePort => Port ?? Protocol.Ssh.DefaultPort();

    public JumpHost Clone() => new()
    {
        Host = Host,
        Username = Username,
        Port = Port,
    };

    public override string ToString() =>
        Username is null ? $"{Host}:{EffectivePort}" : $"{Username}@{Host}:{EffectivePort}";
}
