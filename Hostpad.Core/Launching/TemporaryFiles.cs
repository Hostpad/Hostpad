namespace Hostpad.Core.Launching;

/// <summary>
/// Scratch files for tools that only accept configuration on disk, such as
/// mstsc and VNC viewers. They can hold credentials, so they are written under
/// the user's temp directory and deleted once the tool has read them.
/// </summary>
public static class TemporaryFiles
{
    public static string Write(string prefix, string extension, string contents, string? directory = null)
    {
        var folder = string.IsNullOrWhiteSpace(directory) ? Path.GetTempPath() : directory;
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{prefix}-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, contents);

        return path;
    }

    /// <summary>Deletes a scratch file, ignoring the case where it is already gone.</summary>
    public static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp file is not worth interrupting the user over.
        }
    }
}
