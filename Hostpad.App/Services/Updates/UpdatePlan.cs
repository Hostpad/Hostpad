using System.Globalization;

namespace Hostpad.App.Services.Updates;

public enum UpdatePlanKind
{
    /// <summary>Download, verify and replace the files, then restart.</summary>
    Install,

    /// <summary>Close and let Scoop or winget do it.</summary>
    PackageManager,

    /// <summary>Nothing Hostpad can safely do itself: send the user to the release page.</summary>
    OpenPage,
}

/// <summary>
/// What pressing Update will do for this copy of Hostpad, decided apart from
/// doing it so each case can be tested without a network or a real install.
/// </summary>
public sealed record UpdatePlan(UpdatePlanKind Kind, InstallSource Source, string ButtonText, string Explanation, string Confirmation)
{
    public static UpdatePlan For(ReleaseInfo release) =>
        Create(
            release,
            UpdateService.CurrentDistribution,
            InstallKind.Detect(Environment.ProcessPath ?? string.Empty, Environment.GetEnvironmentVariable));

    public static UpdatePlan Create(ReleaseInfo release, Distribution distribution, InstallSource source)
    {
        ArgumentNullException.ThrowIfNull(release);

        var version = release.Version.ToString(3);

        // A package manager has to do its own updates, or it goes on believing
        // the old version is installed and puts it back on the next upgrade.
        if (InstallKind.UpdateCommand(source) is { } command)
        {
            var manager = source == InstallSource.Scoop ? "Scoop" : "winget";

            return new UpdatePlan(
                UpdatePlanKind.PackageManager,
                source,
                $"Update with {manager}",
                $"It was installed with {manager}, which will do the update.",
                $"Hostpad will close and run this in a console window:\n\n    {command}\n\n" +
                "It starts again once the update has finished.");
        }

        if (release.AssetFor(distribution) is not { Sha256: not null } asset)
        {
            var reason = distribution == Distribution.Unknown
                ? "This is a local build, which does not update itself."
                : "This release has nothing Hostpad can verify for this kind of download.";

            return new UpdatePlan(UpdatePlanKind.OpenPage, source, "Open the release page", reason, string.Empty);
        }

        var megabytes = (asset.Size / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.CurrentCulture);

        return new UpdatePlan(
            UpdatePlanKind.Install,
            source,
            $"Update to {version}",
            "Hostpad can download and install it, and restart.",
            $"Download {asset.Name} ({megabytes} MB) from GitHub and install it?\n\n" +
            "The file is checked against the SHA-256 GitHub recorded when the release was published, " +
            "and discarded if it does not match. Hostpad then restarts; your connections are not touched.");
    }
}
