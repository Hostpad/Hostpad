using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Hostpad.Core.Storage;

namespace Hostpad.App.Services;

/// <summary>
/// Owns the open document and the settings for the lifetime of the window, and
/// is the only place that touches disk. Keeping saves behind one type means the
/// view models never have to think about protection or file paths.
/// </summary>
public sealed class VaultSession
{
    private readonly SettingsStore _settingsStore;
    private DocumentStore _documentStore;

    /// <summary>
    /// Kept so saves can rewrap with the same protection. Held in memory only
    /// while Hostpad runs, and never written anywhere but into the envelope.
    /// </summary>
    private string? _password;

    public VaultSession()
        : this(null, null)
    {
    }

    /// <summary>
    /// A session pointed at files other than the user's own. Demo mode needs it:
    /// redirecting an ordinary session with <see cref="UseVaultAt"/> writes the
    /// new path into the shared settings, so a throwaway vault would go on being
    /// opened at every launch afterwards, hiding the real connection list.
    /// </summary>
    /// <param name="settingsPath">Null for the user's own settings file.</param>
    /// <param name="documentPath">Null to use whatever the settings name.</param>
    public VaultSession(string? settingsPath, string? documentPath)
    {
        _settingsStore = new SettingsStore(settingsPath);
        Settings = _settingsStore.Load();
        _documentStore = new DocumentStore(documentPath ?? Settings.DocumentPath);
        Document = new HostpadDocument();
    }

    public AppSettings Settings { get; }

    public HostpadDocument Document { get; private set; }

    public string VaultPath => _documentStore.Path;

    /// <summary>True when the vault carries a master password, whether or not it is prompted for.</summary>
    public bool HasMasterPassword { get; private set; }

    /// <summary>True when the file on disk cannot be opened without a password.</summary>
    public bool RequiresPassword => _documentStore.Exists && _documentStore.RequiresPassword();

    /// <summary>
    /// Loads the vault, creating an empty one on first run. A brand new vault is
    /// protected by DPAPI alone: no prompt, but useless to anyone who copies the
    /// file off this machine.
    /// </summary>
    public void Open(string? password = null)
    {
        // The vault's own directory, not the fixed one: a session pointed
        // elsewhere must not have to create the user's data folder to open.
        var directory = System.IO.Path.GetDirectoryName(_documentStore.Path);

        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        if (!_documentStore.Exists)
        {
            Document = new HostpadDocument();
            _password = string.IsNullOrEmpty(password) ? null : password;
            HasMasterPassword = _password is not null;
            Save();
            return;
        }

        Document = _documentStore.Load(password);
        _password = string.IsNullOrEmpty(password) ? null : password;
        HasMasterPassword = _documentStore.ProtectionInfo().HasPassword;
    }

    public void Save() => _documentStore.Save(Document, CurrentProtection());

    public void SaveSettings() => _settingsStore.Save(Settings);

    /// <summary>
    /// Sets, changes or clears the master password. Passing null clears it,
    /// which leaves the vault protected by DPAPI alone and therefore readable
    /// only on this machine.
    /// </summary>
    public void SetMasterPassword(string? newPassword)
    {
        var protection = string.IsNullOrEmpty(newPassword)
            ? VaultProtection.DpapiOnly
            : VaultProtection.WithPassword(newPassword);

        _documentStore.ChangeProtection(_password, protection);

        _password = string.IsNullOrEmpty(newPassword) ? null : newPassword;
        HasMasterPassword = _password is not null;
    }

    /// <summary>Points the session at a different vault file, used by Options.</summary>
    public void UseVaultAt(string? path)
    {
        Settings.DocumentPath = path;
        _documentStore = new DocumentStore(path);
    }

    /// <summary>
    /// DPAPI is always applied so the everyday case needs no prompt; the
    /// password is added on top when there is one, which is what makes the file
    /// openable on another machine.
    /// </summary>
    private VaultProtection CurrentProtection() =>
        _password is null ? VaultProtection.DpapiOnly : VaultProtection.WithPassword(_password);
}
