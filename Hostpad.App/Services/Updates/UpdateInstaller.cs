using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Hostpad.App.Services.Updates;

/// <summary>
/// Replaces this copy of Hostpad with a newer release and restarts it.
/// <para>
/// Windows will not overwrite a running executable, but it will rename one.
/// So each file being replaced is first moved aside with a suffix, the new one
/// goes in under the original name, and the leftovers are deleted the next
/// time Hostpad starts, by which point nothing holds them open.
/// </para>
/// </summary>
public static class UpdateInstaller
{
    private const string WaitForArgument = "--wait-for";

    /// <summary>Beside the executable, so the swap is a rename on one volume and a folder Hostpad cannot write to fails before anything is touched.</summary>
    internal const string StagingFolderName = ".hostpad-update";

    internal const string BackupSuffix = ".hostpad-old";

    private static string ExecutablePath =>
        Environment.ProcessPath ?? throw new UpdateException("Hostpad cannot tell where its own executable is.");

    private static string InstallDirectory =>
        Path.GetDirectoryName(ExecutablePath) ?? throw new UpdateException("Hostpad cannot tell where its own executable is.");

    /// <summary>
    /// Called first thing at startup. After an update the new process is
    /// started while the old one is still closing, and would otherwise find the
    /// single-instance lock taken and exit. It waits for the old process here,
    /// then removes what the update left behind.
    /// </summary>
    public static void WaitForPreviousInstance(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var index = Array.IndexOf(args, WaitForArgument);

        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var processId))
        {
            try
            {
                using var previous = Process.GetProcessById(processId);
                previous.WaitForExit(TimeSpan.FromSeconds(30));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                // Already gone.
            }
        }

        try
        {
            // Only the zip build has files in subfolders. A single-file build
            // often sits in a folder full of other tools, which is not worth
            // walking on every start.
            CleanUp(InstallDirectory, recursive: UpdateService.CurrentDistribution == Distribution.Folder);
        }
        catch (UpdateException)
        {
            // Nothing to clean when the location is unknown.
        }
    }

    /// <summary>
    /// Downloads, verifies and swaps in the release that matches this build.
    /// Leaves the running process untouched apart from renaming its files; the
    /// caller restarts.
    /// </summary>
    /// <exception cref="UpdateException">Anything that stops the update. The installation is left as it was.</exception>
    public static async Task InstallAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(release);

        var asset = release.AssetFor(UpdateService.CurrentDistribution)
                    ?? throw new UpdateException("This release has no download matching this copy of Hostpad.");

        var staging = Path.Combine(InstallDirectory, StagingFolderName);
        var files = Path.Combine(staging, "files");

        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            Directory.CreateDirectory(files);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException(
                $"Hostpad cannot write to {InstallDirectory}, so it cannot update itself there. Download the new version from the release page.",
                exception);
        }

        try
        {
            if (UpdateService.CurrentDistribution == Distribution.Folder)
            {
                var archive = Path.Combine(staging, asset.Name);
                await UpdateService.DownloadVerifiedAsync(asset, archive, progress, cancellation).ConfigureAwait(false);

                // ExtractToDirectory refuses entries that would land outside
                // the destination, so a crafted archive cannot escape it.
                await Task.Run(() => ZipFile.ExtractToDirectory(archive, files), cancellation).ConfigureAwait(false);
            }
            else
            {
                // Keeps whatever name the user gave the executable.
                var target = Path.Combine(files, Path.GetFileName(ExecutablePath));
                await UpdateService.DownloadVerifiedAsync(asset, target, progress, cancellation).ConfigureAwait(false);
            }

            ReplaceFiles(files, InstallDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new UpdateException($"The update could not be installed: {exception.Message}", exception);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>
    /// Moves every file under <paramref name="sourceDirectory"/> into
    /// <paramref name="targetDirectory"/>, keeping the old ones aside. All or
    /// nothing: if one move fails, the ones already done are undone, so a
    /// half-replaced installation is never left behind.
    /// </summary>
    public static void ReplaceFiles(string sourceDirectory, string targetDirectory)
    {
        var done = new Stack<(string Target, string? Backup, bool Placed)>();

        try
        {
            foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                string? backup = null;

                if (File.Exists(target))
                {
                    backup = target + BackupSuffix;
                    File.Delete(backup);
                    File.Move(target, backup);
                }

                done.Push((target, backup, Placed: false));
                File.Move(source, target);

                done.Pop();
                done.Push((target, backup, Placed: true));
            }
        }
        catch
        {
            while (done.Count > 0)
            {
                var (target, backup, placed) = done.Pop();

                if (placed)
                {
                    File.Delete(target);
                }

                if (backup is not null)
                {
                    File.Move(backup, target);
                }
            }

            throw;
        }
    }

    /// <summary>Deletes the set-aside files and the staging folder an earlier update left.</summary>
    public static void CleanUp(string directory, bool recursive)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        TryDeleteDirectory(Path.Combine(directory, StagingFolderName));

        try
        {
            foreach (var leftover in Directory.EnumerateFiles(
                         directory,
                         "*" + BackupSuffix,
                         recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                UpdateService.TryDelete(leftover);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Tried again on the next start.
        }
    }

    /// <summary>Starts the updated executable, which waits for this process to exit before it takes over.</summary>
    public static void StartUpdatedInstance()
    {
        var start = new ProcessStartInfo(ExecutablePath) { UseShellExecute = false };
        start.ArgumentList.Add(WaitForArgument);
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Process.Start(start)?.Dispose();
    }

    /// <summary>
    /// Opens a console that waits for Hostpad to close, runs the package
    /// manager's own update, and starts Hostpad again. The console stays open
    /// if the update fails, so the reason can be read.
    /// </summary>
    public static void StartPackageManagerUpdate(InstallSource source)
    {
        var command = InstallKind.UpdateCommand(source)
                      ?? throw new UpdateException("This copy of Hostpad was not installed by a package manager.");

        var script = BuildPackageManagerScript(
            command,
            Environment.ProcessId,
            RelaunchPath(source, ExecutablePath));

        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        Process.Start(start)?.Dispose();
    }

    internal static string BuildPackageManagerScript(string command, int processId, string relaunchPath)
    {
        var quoted = "'" + relaunchPath.Replace("'", "''", StringComparison.Ordinal) + "'";

        return $$"""
            $Host.UI.RawUI.WindowTitle = 'Updating Hostpad'
            Write-Host 'Waiting for Hostpad to close...'
            Wait-Process -Id {{processId}} -ErrorAction SilentlyContinue
            {{command}}
            if ($?) {
                Start-Process -FilePath {{quoted}}
                Start-Sleep -Seconds 3
            } else {
                Write-Host ''
                Read-Host 'The update did not complete. Press Enter to close'
            }
            """;
    }

    /// <summary>
    /// Scoop installs each version in its own folder and points "current" at
    /// the newest, so the relaunch goes through "current" to reach the new one.
    /// winget replaces the file in place.
    /// </summary>
    internal static string RelaunchPath(InstallSource source, string executablePath)
    {
        if (source != InstallSource.Scoop)
        {
            return executablePath;
        }

        var versionDirectory = Path.GetDirectoryName(executablePath);
        var appDirectory = Path.GetDirectoryName(versionDirectory);

        return appDirectory is null
            ? executablePath
            : Path.Combine(appDirectory, "current", Path.GetFileName(executablePath));
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleared on the next start.
        }
    }
}
