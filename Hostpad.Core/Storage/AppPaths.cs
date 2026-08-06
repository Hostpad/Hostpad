namespace Hostpad.Core.Storage;

/// <summary>
/// Where Hostpad keeps its files: a <c>.hostpad</c> folder in the user profile,
/// following the convention of .ssh, .aws and friends.
/// <para>
/// AutoPuTTY kept its list next to the executable, which breaks as soon as the
/// program lives in Program Files and ties the data to one copy of the tool.
/// Keeping it in the profile means the data survives moving or reinstalling
/// Hostpad, and stays reachable by hand when needed.
/// </para>
/// </summary>
public static class AppPaths
{
    public const string DataDirectoryName = ".hostpad";

    public const string VaultFileName = "connections.hpx";

    public const string SettingsFileName = "settings.json";

    /// <summary>Suggested extension for exported, password-protected vaults.</summary>
    public const string ExportExtension = ".hpx";

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DataDirectoryName);

    public static string DefaultVaultPath => Path.Combine(DataDirectory, VaultFileName);

    public static string SettingsPath => Path.Combine(DataDirectory, SettingsFileName);

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);

    /// <summary>
    /// Where AutoPuTTY kept its server list, offered as a starting point for
    /// "Import from AutoPuTTY". Returns null when nothing is found.
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
