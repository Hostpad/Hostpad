using System.Globalization;
using System.Text.Json;

namespace Hostpad.App.Services.Updates;

/// <summary>One file attached to a release, with the hash GitHub computed when it was uploaded.</summary>
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string? Sha256);

/// <summary>The latest published release, as far as the update check needs to know it.</summary>
public sealed record ReleaseInfo(Version Version, Uri PageUrl, IReadOnlyList<ReleaseAsset> Assets)
{
    /// <summary>
    /// Reads the GitHub API's "latest release" response. Returns null for
    /// anything unexpected rather than throwing: a check that fails is meant to
    /// fail quietly, never to put a dialog in front of the user.
    /// </summary>
    public static ReleaseInfo? FromGitHubJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean() ||
                root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean())
            {
                return null;
            }

            if (AppVersion.TryParse(root.GetProperty("tag_name").GetString(), out var version) is false ||
                !Uri.TryCreate(root.GetProperty("html_url").GetString(), UriKind.Absolute, out var page))
            {
                return null;
            }

            var assets = new List<ReleaseAsset>();

            if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in list.EnumerateArray())
                {
                    if (ReadAsset(asset) is { } parsed)
                    {
                        assets.Add(parsed);
                    }
                }
            }

            return new ReleaseInfo(version, page, assets);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Picks the file that replaces the build this copy of Hostpad came from.</summary>
    public ReleaseAsset? AssetFor(Distribution distribution)
    {
        var name = distribution switch
        {
            Distribution.Folder => string.Create(CultureInfo.InvariantCulture, $"Hostpad-{Version.ToString(3)}-win-x64.zip"),
            Distribution.Standalone => string.Create(CultureInfo.InvariantCulture, $"Hostpad-{Version.ToString(3)}-win-x64.exe"),
            Distribution.SingleFile => "Hostpad.exe",
            _ => null,
        };

        return name is null
            ? null
            : Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static ReleaseAsset? ReadAsset(JsonElement asset)
    {
        var name = asset.GetProperty("name").GetString();

        if (string.IsNullOrEmpty(name) ||
            !Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var size = asset.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;

        // "sha256:<hex>", computed by GitHub on upload. Anything else is
        // treated as no hash at all, and an asset with no hash is never installed.
        string? sha256 = null;
        if (asset.TryGetProperty("digest", out var digest) &&
            digest.ValueKind == JsonValueKind.String &&
            digest.GetString() is { } value &&
            value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) &&
            value.Length == "sha256:".Length + 64)
        {
            sha256 = value["sha256:".Length..].ToLowerInvariant();
        }

        return new ReleaseAsset(name, url, size, sha256);
    }
}

/// <summary>Which of the three downloads this copy of Hostpad is. Stamped in by the release workflow.</summary>
public enum Distribution
{
    /// <summary>A local or development build: nothing to replace it with.</summary>
    Unknown,

    /// <summary>The zip: framework-dependent, a folder of files.</summary>
    Folder,

    /// <summary>The large single file that carries its own runtime.</summary>
    Standalone,

    /// <summary>The small single file that uses the installed runtime.</summary>
    SingleFile,
}

/// <summary>Version numbers as the update check compares them.</summary>
public static class AppVersion
{
    /// <summary>
    /// Accepts "v1.2.3", "1.2.3", and the informational form "1.2.3+abc1234".
    /// A pre-release suffix ("1.2.3-beta") is refused: it is not a release
    /// anyone should be moved to, or away from, automatically.
    /// <para>
    /// Comparison is numeric, never textual, so 1.0.10 is newer than 1.0.9.
    /// </para>
    /// </summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim();

        if (core.StartsWith('v') || core.StartsWith('V'))
        {
            core = core[1..];
        }

        var plus = core.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            core = core[..plus];
        }

        if (core.Contains('-', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = core.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new Version(numbers[0], numbers[1], numbers[2]);
        return true;
    }
}
