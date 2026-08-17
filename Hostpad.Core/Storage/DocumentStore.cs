using System.Text.Json;
using Hostpad.Core.Model;
using Hostpad.Core.Security;

namespace Hostpad.Core.Storage;

/// <summary>
/// Reads and writes the encrypted document. Encryption belongs to
/// <see cref="Vault"/>; this type only deals with files and schema versions.
/// </summary>
public sealed class DocumentStore
{
    private readonly string _path;

    public DocumentStore(string? path = null)
    {
        _path = path ?? AppPaths.DefaultVaultPath;
    }

    public string Path => _path;

    public bool Exists => File.Exists(_path);

    /// <summary>
    /// True when opening the file will require a master password. Reads only the
    /// envelope header, so the UI can decide whether to prompt before unlocking.
    /// </summary>
    /// <exception cref="VaultFormatException">Not a vault, corrupted, or written by a newer build.</exception>
    public bool RequiresPassword()
    {
        var envelope = ReadEnvelope();
        return envelope.HasPassword && !CanOpenWithDpapi(envelope);
    }

    /// <summary>How the file on disk is protected. Reads the header only.</summary>
    /// <exception cref="VaultFormatException">Not a vault, corrupted, or written by a newer build.</exception>
    public (bool HasPassword, bool HasDpapi) ProtectionInfo()
    {
        var envelope = ReadEnvelope();
        return (envelope.HasPassword, envelope.HasDpapi);
    }

    /// <exception cref="VaultAuthenticationException">Wrong or missing master password.</exception>
    /// <exception cref="VaultFormatException">Not a vault, corrupted, or written by a newer build.</exception>
    public HostpadDocument Load(string? password = null)
    {
        var envelope = ReadEnvelope();
        var plaintext = Vault.Open(envelope, password);

        try
        {
            var document = HostpadJson.Deserialize<HostpadDocument>(plaintext, HostpadJson.PayloadOptions);
            return Migrate(document);
        }
        catch (JsonException ex)
        {
            throw new VaultFormatException("The vault decrypted but its contents are not valid JSON.", ex);
        }
        finally
        {
            Array.Clear(plaintext);
        }
    }

    public void Save(HostpadDocument document, VaultProtection protection)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.SchemaVersion = HostpadDocument.CurrentSchemaVersion;

        var plaintext = HostpadJson.SerializeToUtf8Bytes(document, HostpadJson.PayloadOptions);
        try
        {
            var envelope = Vault.Seal(plaintext, protection);
            WriteEnvelope(envelope);
        }
        finally
        {
            Array.Clear(plaintext);
        }
    }

    /// <summary>
    /// Sets, changes or clears the master password. The document is not
    /// decrypted, so this stays instant regardless of how large the vault is.
    /// </summary>
    public void ChangeProtection(string? currentPassword, VaultProtection newProtection)
    {
        var rewrapped = Vault.Rewrap(ReadEnvelope(), currentPassword, newProtection);
        WriteEnvelope(rewrapped);
    }

    private VaultEnvelope ReadEnvelope()
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException("No Hostpad vault at this location.", _path);
        }

        var bytes = File.ReadAllBytes(_path);

        VaultEnvelope envelope;

        try
        {
            envelope = HostpadJson.Deserialize<VaultEnvelope>(bytes, HostpadJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new VaultFormatException($"'{_path}' is not a readable Hostpad vault.", ex);
        }

        // Checked here rather than only in Vault.Open, so that the header-only
        // callers above refuse a file from a newer build instead of guessing at
        // its protection and prompting for a password that would never work.
        Vault.VerifyFormat(envelope);

        return envelope;
    }

    private void WriteEnvelope(VaultEnvelope envelope)
    {
        var bytes = HostpadJson.SerializeToUtf8Bytes(envelope, HostpadJson.Options);
        AtomicFile.WriteAllBytes(_path, bytes);
    }

    private static bool CanOpenWithDpapi(VaultEnvelope envelope)
    {
        if (!envelope.HasDpapi || !OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var key = DpapiKeyProtector.Unprotect(envelope.DpapiWrappedKey!);
            Array.Clear(key);
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Upgrades a document written by an older build. Nothing to do at schema
    /// version 1; the branch exists so the first real migration has an obvious home.
    /// </summary>
    private static HostpadDocument Migrate(HostpadDocument document)
    {
        if (document.SchemaVersion > HostpadDocument.CurrentSchemaVersion)
        {
            throw new VaultFormatException(
                $"This vault uses schema version {document.SchemaVersion}; " +
                $"this build understands up to {HostpadDocument.CurrentSchemaVersion}. Update Hostpad.");
        }

        return document;
    }
}
