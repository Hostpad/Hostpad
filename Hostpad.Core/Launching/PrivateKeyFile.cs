using Hostpad.Core.Model;

namespace Hostpad.Core.Launching;

/// <summary>What a private key file looks like from its first line.</summary>
public enum PrivateKeyFormat
{
    /// <summary>Missing, unreadable, or not a format recognised here.</summary>
    Unknown,

    /// <summary>PuTTY's own format, which PuTTY and WinSCP load directly.</summary>
    Ppk,

    /// <summary>OpenSSH or PEM, as ssh-keygen writes and cloud providers hand out.</summary>
    OpenSsh,
}

/// <summary>Chooses the key a launch uses, and recognises key formats for the editor.</summary>
public static class PrivateKeyFile
{
    /// <summary>
    /// A key set on the connection always wins, whatever the global switch
    /// says. The case it exists for is a customer handing over a key for one
    /// server, and having to also remember a global toggle would defeat it. The
    /// global key covers only the connections that have none of their own.
    /// </summary>
    public static string? Resolve(Credential credential, bool useGlobalKey, string? globalKeyPath)
    {
        ArgumentNullException.ThrowIfNull(credential);

        if (credential.HasKey)
        {
            return credential.PrivateKeyPath!.Trim();
        }

        return useGlobalKey && !string.IsNullOrWhiteSpace(globalKeyPath) ? globalKeyPath : null;
    }

    /// <summary>
    /// Reads only the start of the file. Never throws: the editor calls this
    /// while the path is being typed, when it is usually not a file yet.
    /// </summary>
    public static PrivateKeyFormat DetectFormat(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PrivateKeyFormat.Unknown;
        }

        try
        {
            using var reader = new StreamReader(path.Trim());
            var firstLine = reader.ReadLine() ?? string.Empty;

            if (firstLine.StartsWith("PuTTY-User-Key-File-", StringComparison.Ordinal))
            {
                return PrivateKeyFormat.Ppk;
            }

            return firstLine.StartsWith("-----BEGIN ", StringComparison.Ordinal)
                ? PrivateKeyFormat.OpenSsh
                : PrivateKeyFormat.Unknown;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return PrivateKeyFormat.Unknown;
        }
    }
}
