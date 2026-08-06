namespace Hostpad.Core.Storage;

/// <summary>
/// Where Hostpad keeps its files. Roaming AppData is used so the vault follows
/// a user across domain machines, which is the behaviour sysadmins expect.
/// </summary>
public static class AppPaths
{
    public const string VaultFileName = "connections.hpx";

    public const string SettingsFileName = "settings.json";

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hostpad");

    public static string DefaultVaultPath => Path.Combine(DataDirectory, VaultFileName);

    public static string SettingsPath => Path.Combine(DataDirectory, SettingsFileName);

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);

    /// <summary>
    /// Where AutoPuTTY kept its server list, checked on first run so an existing
    /// setup can be imported. Returns null when nothing is found.
    /// </summary>
    public static string? FindLegacyAutoPuttyFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "autoputty.xml"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoPuTTY",
                "autoputty.xml"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
