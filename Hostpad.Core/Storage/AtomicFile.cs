namespace Hostpad.Core.Storage;

/// <summary>
/// Write-to-temp-then-replace, so a crash or a full disk mid-save cannot leave a
/// half-written vault behind. Losing a server list to a truncated file is the
/// kind of failure users never forgive.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllBytes(temporaryPath, contents);

        if (File.Exists(path))
        {
            // Keeps the previous version reachable until the swap succeeds.
            var backupPath = path + ".bak";
            File.Replace(temporaryPath, path, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, path);
        }
    }
}
