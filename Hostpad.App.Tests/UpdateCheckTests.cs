using Hostpad.App.Services.Updates;
using Xunit;

namespace Hostpad.App.Tests;

public sealed class UpdateCheckTests
{
    private const string Hash = "79115efbd78013f1504b54a432d4dd6b185a8f11ea82e99c75594c0f9e9ea7e3";

    private static string ReleaseJson(
        string tag = "v1.1.0",
        bool prerelease = false,
        string digest = "sha256:" + Hash) => $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "https://github.com/Hostpad/Hostpad/releases/tag/{{tag}}",
          "draft": false,
          "prerelease": {{(prerelease ? "true" : "false")}},
          "assets": [
            { "name": "Hostpad-1.1.0-win-x64.zip", "size": 7340032, "digest": "{{digest}}",
              "browser_download_url": "https://github.com/Hostpad/Hostpad/releases/download/v1.1.0/Hostpad-1.1.0-win-x64.zip" },
            { "name": "Hostpad-1.1.0-win-x64.exe", "size": 70254592, "digest": "{{digest}}",
              "browser_download_url": "https://github.com/Hostpad/Hostpad/releases/download/v1.1.0/Hostpad-1.1.0-win-x64.exe" },
            { "name": "Hostpad.exe", "size": 7340032, "digest": "{{digest}}",
              "browser_download_url": "https://github.com/Hostpad/Hostpad/releases/download/v1.1.0/Hostpad.exe" },
            { "name": "insecure.exe", "size": 1, "digest": "{{digest}}",
              "browser_download_url": "http://example.com/insecure.exe" }
          ]
        }
        """;

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("1.0.5+5c93f49abcdef", 1, 0, 5)]
    [InlineData(" V10.0.0 ", 10, 0, 0)]
    public void ParsesReleaseAndBuildVersions(string text, int major, int minor, int patch)
    {
        Assert.True(AppVersion.TryParse(text, out var version));
        Assert.Equal(new Version(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-beta")]
    [InlineData("v1.x.3")]
    [InlineData("-1.2.3")]
    public void RefusesWhatIsNotAPlainRelease(string? text) =>
        Assert.False(AppVersion.TryParse(text, out _));

    [Fact]
    public void ComparesNumericallyNotAsText()
    {
        AppVersion.TryParse("1.0.10", out var ten);
        AppVersion.TryParse("1.0.9", out var nine);

        Assert.True(ten > nine);
    }

    [Fact]
    public void ReadsTheLatestReleaseFromGitHub()
    {
        var release = ReleaseInfo.FromGitHubJson(ReleaseJson());

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 1, 0), release.Version);
        Assert.Equal("https://github.com/Hostpad/Hostpad/releases/tag/v1.1.0", release.PageUrl.ToString());
        Assert.Equal(Hash, release.Assets[0].Sha256);
    }

    [Fact]
    public void DropsAssetsThatAreNotServedOverHttps()
    {
        var release = ReleaseInfo.FromGitHubJson(ReleaseJson())!;

        Assert.DoesNotContain(release.Assets, a => a.Name == "insecure.exe");
    }

    [Theory]
    [InlineData("md5:abc")]
    [InlineData("sha256:tooshort")]
    [InlineData("")]
    public void TreatsAnythingButAFullSha256AsNoHash(string digest)
    {
        var release = ReleaseInfo.FromGitHubJson(ReleaseJson(digest: digest))!;

        Assert.All(release.Assets, a => Assert.Null(a.Sha256));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "tag_name": "nightly", "html_url": "https://github.com/x" }""")]
    public void ReturnsNullRatherThanThrowingOnUnexpectedResponses(string json) =>
        Assert.Null(ReleaseInfo.FromGitHubJson(json));

    [Fact]
    public void IgnoresPreReleases() =>
        Assert.Null(ReleaseInfo.FromGitHubJson(ReleaseJson(prerelease: true)));

    [Theory]
    [InlineData(Distribution.Folder, "Hostpad-1.1.0-win-x64.zip")]
    [InlineData(Distribution.Standalone, "Hostpad-1.1.0-win-x64.exe")]
    [InlineData(Distribution.SingleFile, "Hostpad.exe")]
    public void PicksTheSameKindOfDownloadAsTheRunningCopy(Distribution distribution, string expected) =>
        Assert.Equal(expected, ReleaseInfo.FromGitHubJson(ReleaseJson())!.AssetFor(distribution)?.Name);

    [Fact]
    public void PicksNothingForALocalBuild() =>
        Assert.Null(ReleaseInfo.FromGitHubJson(ReleaseJson())!.AssetFor(Distribution.Unknown));

    [Fact]
    public void Plan_InstallsAVerifiableDownload()
    {
        var plan = UpdatePlan.Create(ReleaseInfo.FromGitHubJson(ReleaseJson())!, Distribution.Standalone, InstallSource.Manual);

        Assert.Equal(UpdatePlanKind.Install, plan.Kind);
        Assert.Contains("SHA-256", plan.Confirmation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstallSource.Scoop, "scoop update hostpad")]
    [InlineData(InstallSource.Winget, "winget upgrade --id Hostpad.Hostpad --exact")]
    public void Plan_LeavesPackageManagersToDoTheirOwnUpdates(InstallSource source, string command)
    {
        var plan = UpdatePlan.Create(ReleaseInfo.FromGitHubJson(ReleaseJson())!, Distribution.Standalone, source);

        Assert.Equal(UpdatePlanKind.PackageManager, plan.Kind);
        Assert.Contains(command, plan.Confirmation, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_NeverInstallsWhatCannotBeVerified()
    {
        var plan = UpdatePlan.Create(ReleaseInfo.FromGitHubJson(ReleaseJson(digest: ""))!, Distribution.Standalone, InstallSource.Manual);

        Assert.Equal(UpdatePlanKind.OpenPage, plan.Kind);
    }

    [Fact]
    public void Plan_SendsLocalBuildsToTheReleasePage()
    {
        var plan = UpdatePlan.Create(ReleaseInfo.FromGitHubJson(ReleaseJson())!, Distribution.Unknown, InstallSource.Manual);

        Assert.Equal(UpdatePlanKind.OpenPage, plan.Kind);
    }
}
