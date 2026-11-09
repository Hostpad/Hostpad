using System.IO;

namespace Hostpad.App.Services.Updates;

/// <summary>Who put this copy of Hostpad where it is, and so who should replace it.</summary>
public enum InstallSource
{
    /// <summary>Downloaded by hand: Hostpad replaces its own files.</summary>
    Manual,

    /// <summary>Installed by Scoop, which must do the update or lose track of the version.</summary>
    Scoop,

    /// <summary>Installed by winget as a portable package; the same applies.</summary>
    Winget,
}

public static class InstallKind
{
    /// <summary>
    /// Works from where the executable lives. Scoop keeps applications under
    /// <c>&lt;root&gt;\apps\&lt;name&gt;\</c>, with the root moved by the SCOOP
    /// or SCOOP_GLOBAL variables; winget unpacks portable packages under a
    /// <c>WinGet\Packages\&lt;id&gt;_&lt;source&gt;</c> folder, per user or per machine.
    /// </summary>
    public static InstallSource Detect(string executablePath, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return InstallSource.Manual;
        }

        var path = Normalise(executablePath);

        if (path.Contains(@"\winget\packages\hostpad.hostpad_", StringComparison.OrdinalIgnoreCase))
        {
            return InstallSource.Winget;
        }

        var scoopRoots = new[]
        {
            environment("SCOOP"),
            environment("SCOOP_GLOBAL"),
            environment("USERPROFILE") is { } profile ? Path.Combine(profile, "scoop") : null,
            environment("ProgramData") is { } data ? Path.Combine(data, "scoop") : null,
        };

        foreach (var root in scoopRoots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var apps = Normalise(Path.Combine(root!, "apps")) + @"\";

            if (path.StartsWith(apps, StringComparison.OrdinalIgnoreCase))
            {
                return InstallSource.Scoop;
            }
        }

        return InstallSource.Manual;
    }

    /// <summary>
    /// The command that updates Hostpad through its package manager, run in a
    /// console the user can see. Null for a manual install.
    /// </summary>
    public static string? UpdateCommand(InstallSource source) => source switch
    {
        InstallSource.Scoop => "scoop update hostpad",
        InstallSource.Winget => "winget upgrade --id Hostpad.Hostpad --exact",
        _ => null,
    };

    private static string Normalise(string path) => path.Replace('/', '\\').TrimEnd('\\');
}
