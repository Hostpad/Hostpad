namespace Hostpad.Core.Model;

/// <summary>
/// Authentication material for a connection.
/// <para>
/// Secrets live here in plaintext at runtime; confidentiality comes from the
/// document being encrypted as a whole before it touches disk. Nothing in this
/// type may be written to a log, an exception message, or a crash dump.
/// </para>
/// </summary>
public sealed class Credential
{
    public string? Username { get; set; }

    /// <summary>Plaintext only in memory. Never serialize outside the encrypted document.</summary>
    public string? Password { get; set; }

    /// <summary>Path to a private key file (PuTTY .ppk or OpenSSH), when key auth is used.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>Delegate authentication to Pageant / ssh-agent instead of an explicit key.</summary>
    public bool UseAgent { get; set; }

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public bool HasKey => !string.IsNullOrWhiteSpace(PrivateKeyPath);

    public Credential Clone() => new()
    {
        Username = Username,
        Password = Password,
        PrivateKeyPath = PrivateKeyPath,
        UseAgent = UseAgent,
    };

    /// <summary>Deliberately hides every field: keeps secrets out of logs and debuggers.</summary>
    public override string ToString() => $"Credential({Username ?? "<none>"})";
}
