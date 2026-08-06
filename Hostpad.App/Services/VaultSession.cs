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
    private VaultProtection _protection = VaultProtection.DpapiOnly;

    public VaultSession()
    {
        _settingsStore = new SettingsStore();
        Settings = _settingsStore.Load();
        _documentStore = new DocumentStore(Settings.DocumentPath);
        Document = new HostpadDocument();
    }

    public AppSettings Settings { get; }

    public HostpadDocument Document { get; private set; }

    public string VaultPath => _documentStore.Path;

    /// <summary>True when the file on disk asks for a master password.</summary>
    public bool RequiresPassword => _documentStore.Exists && _documentStore.RequiresPassword();

    /// <summary>
    /// Loads the vault, creating an empty one on first run. A brand new vault is
    /// protected by DPAPI alone: no prompt, but useless to anyone who copies the
    /// file off this machine.
    /// </summary>
    public void Open(string? password = null)
    {
        AppPaths.EnsureDataDirectory();

        if (!_documentStore.Exists)
        {
            Document = new HostpadDocument();
            _documentStore.Save(Document, _protection);
            return;
        }

        Document = _documentStore.Load(password);
        _protection = string.IsNullOrEmpty(password)
            ? VaultProtection.DpapiOnly
            : VaultProtection.WithPassword(password);
    }

    public void Save() => _documentStore.Save(Document, _protection);

    public void SaveSettings() => _settingsStore.Save(Settings);

    /// <summary>Points the session at a different vault file, used by Options.</summary>
    public void UseVaultAt(string? path)
    {
        Settings.DocumentPath = path;
        _documentStore = new DocumentStore(path);
    }
}
