using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;

namespace Hostpad.App.Services.Updates;

/// <summary>
/// The only code in Hostpad that talks to the network. It runs when the user
/// presses Check for updates, or at startup when they switched that on in
/// Options; nothing else ever calls out.
/// </summary>
public static class UpdateService
{
    private static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/Hostpad/Hostpad/releases/latest");

    /// <summary>The largest file Hostpad ships is about 70 MB; anything far beyond is not ours.</summary>
    private const long MaximumDownloadBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Created on first use, not at startup: most sessions never check, and
    /// static fields initialise in order, so it must not run before the version
    /// properties below it are set.
    /// </summary>
    private static readonly Lazy<HttpClient> LazyHttp = new(CreateClient);

    private static HttpClient Http => LazyHttp.Value;

    /// <summary>The version this process is, as the release workflow stamped it.</summary>
    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    /// <summary>Which of the three downloads this process is. Unknown for local builds.</summary>
    public static Distribution CurrentDistribution { get; } = ReadDistribution();

    /// <summary>
    /// The latest release, or null when it cannot be read: offline, blocked by
    /// a proxy, rate-limited, or anything else. Never throws for those; an
    /// office without internet access is normal, not an error.
    /// </summary>
    public static async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellation = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return ReleaseInfo.FromGitHubJson(json);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads an asset into <paramref name="destination"/> and checks it
    /// against the SHA-256 GitHub recorded when it was uploaded. A file that
    /// does not match is deleted before this throws, so nothing unverified is
    /// ever left where the installer could pick it up.
    /// </summary>
    /// <exception cref="UpdateException">No hash to check against, a mismatch, or a failed download.</exception>
    public static async Task DownloadVerifiedAsync(
        ReleaseAsset asset,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.Sha256 is null)
        {
            throw new UpdateException(
                $"GitHub has no SHA-256 for {asset.Name}, so it cannot be checked. Download it from the release page instead.");
        }

        try
        {
            using var response = await Http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? asset.Size;
            if (total > MaximumDownloadBytes)
            {
                throw new UpdateException($"{asset.Name} is larger than any Hostpad release; it was not downloaded.");
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            await using (var source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false))
            await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
                {
                    received += read;
                    if (received > MaximumDownloadBytes)
                    {
                        throw new UpdateException($"{asset.Name} is larger than any Hostpad release; the download was stopped.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);

                    if (total > 0)
                    {
                        progress?.Report((double)received / total);
                    }
                }
            }

            var actual = Convert.ToHexStringLower(hash.GetHashAndReset());

            if (!string.Equals(actual, asset.Sha256, StringComparison.Ordinal))
            {
                throw new UpdateException(
                    $"The downloaded {asset.Name} does not match the SHA-256 GitHub published for it, so it was discarded. Nothing was changed.");
            }
        }
        catch (Exception exception)
        {
            TryDelete(destination);

            if (exception is UpdateException or OperationCanceledException)
            {
                throw;
            }

            throw new UpdateException($"The download failed: {exception.Message}", exception);
        }
    }

    private static HttpClient CreateClient()
    {
        // No timeout on the client itself: a 70 MB download on a slow line is
        // legitimate. The check sets its own short one.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        // GitHub refuses API requests without a User-Agent. This one names the
        // application and its version, and nothing about the user or machine.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Hostpad", CurrentVersion.ToString(3)));
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(+https://github.com/Hostpad/Hostpad)"));

        return client;
    }

    private static Version ReadCurrentVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return AppVersion.TryParse(informational, out var version)
            ? version
            : Assembly.GetExecutingAssembly().GetName().Version is { } assemblyVersion
                ? new Version(assemblyVersion.Major, assemblyVersion.Minor, Math.Max(assemblyVersion.Build, 0))
                : new Version(0, 0, 0);
    }

    private static Distribution ReadDistribution()
    {
        var stamped = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "HostpadDistribution")?
            .Value;

        return Enum.TryParse<Distribution>(stamped, ignoreCase: true, out var distribution)
            ? distribution
            : Distribution.Unknown;
    }

    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Left behind in the staging folder, which is cleared on the next start.
        }
    }
}

public sealed class UpdateException : Exception
{
    public UpdateException(string message) : base(message)
    {
    }

    public UpdateException(string message, Exception inner) : base(message, inner)
    {
    }
}
