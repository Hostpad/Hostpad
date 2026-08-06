using System.Text.Json;
using Hostpad.Core.Model;

namespace Hostpad.Core.Storage;

/// <summary>
/// Plain-JSON application preferences. Nothing secret lives here, which is what
/// lets the app start and show a window before the vault is unlocked.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsPath;
    }

    public string Path => _path;

    /// <summary>
    /// Returns defaults when the file is missing or unreadable. Corrupt
    /// preferences must never stop the application from starting — the user can
    /// reconfigure paths, but cannot fix a program that refuses to launch.
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            return HostpadJson.Deserialize<AppSettings>(File.ReadAllBytes(_path), HostpadJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        AtomicFile.WriteAllBytes(_path, HostpadJson.SerializeToUtf8Bytes(settings, HostpadJson.Options));
    }
}
