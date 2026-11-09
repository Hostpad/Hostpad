using System.IO;
using Hostpad.App.Services.Updates;
using Xunit;

namespace Hostpad.App.Tests;

public sealed class UpdateInstallerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "Hostpad.Update", Guid.NewGuid().ToString("N"));

    private string Source => Path.Combine(_root, "source");

    private string Target => Path.Combine(_root, "target");

    public UpdateInstallerTests()
    {
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Target);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Write(string directory, string relative, string content)
    {
        var path = Path.Combine(directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void ReplaceFiles_PutsTheNewFilesInAndKeepsTheOldOnesAside()
    {
        Write(Target, "Hostpad.exe", "old");
        Write(Source, "Hostpad.exe", "new");
        Write(Source, @"it\Hostpad.resources.dll", "added");

        UpdateInstaller.ReplaceFiles(Source, Target);

        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "Hostpad.exe")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "Hostpad.exe" + UpdateInstaller.BackupSuffix)));
        Assert.Equal("added", File.ReadAllText(Path.Combine(Target, @"it\Hostpad.resources.dll")));
    }

    [Fact]
    public void ReplaceFiles_UndoesEverythingWhenOneFileCannotBeReplaced()
    {
        Write(Target, "a.dll", "old a");
        Write(Target, "b.dll", "old b");
        Write(Source, "a.dll", "new a");
        Write(Source, "b.dll", "new b");

        // Holding b.dll open without sharing stops it being moved aside,
        // as a file locked by another process would.
        using (new FileStream(Path.Combine(Target, "b.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => UpdateInstaller.ReplaceFiles(Source, Target));
        }

        Assert.Equal("old a", File.ReadAllText(Path.Combine(Target, "a.dll")));
        Assert.Equal("old b", File.ReadAllText(Path.Combine(Target, "b.dll")));
        Assert.Empty(Directory.GetFiles(Target, "*" + UpdateInstaller.BackupSuffix));
    }

    [Fact]
    public void CleanUp_RemovesWhatAnUpdateLeftBehind()
    {
        Write(Target, "Hostpad.exe", "current");
        Write(Target, "Hostpad.exe" + UpdateInstaller.BackupSuffix, "previous");
        Write(Target, @"sub\x.dll" + UpdateInstaller.BackupSuffix, "previous");
        Write(Target, Path.Combine(UpdateInstaller.StagingFolderName, "files", "Hostpad.exe"), "partial");

        UpdateInstaller.CleanUp(Target, recursive: true);

        Assert.True(File.Exists(Path.Combine(Target, "Hostpad.exe")));
        Assert.Empty(Directory.GetFiles(Target, "*" + UpdateInstaller.BackupSuffix, SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(Target, UpdateInstaller.StagingFolderName)));
    }

    [Fact]
    public void CleanUp_StaysInTheTopFolderUnlessAskedOtherwise()
    {
        var nested = @"other-tool\data" + UpdateInstaller.BackupSuffix;
        Write(Target, nested, "not ours to touch");

        UpdateInstaller.CleanUp(Target, recursive: false);

        Assert.True(File.Exists(Path.Combine(Target, nested)));
    }

    [Theory]
    [InlineData(@"C:\Users\denis\scoop\apps\hostpad\current\Hostpad.exe", InstallSource.Scoop)]
    [InlineData(@"C:\Users\denis\scoop\apps\hostpad\1.0.5\Hostpad.exe", InstallSource.Scoop)]
    [InlineData(@"D:\tools\scoop\apps\hostpad\current\Hostpad.exe", InstallSource.Scoop)]
    [InlineData(@"C:\ProgramData\scoop\apps\hostpad\current\Hostpad.exe", InstallSource.Scoop)]
    [InlineData(@"C:\Users\denis\AppData\Local\Microsoft\WinGet\Packages\Hostpad.Hostpad_Microsoft.Winget.Source_8wekyb3d8bbwe\Hostpad-1.0.5-win-x64.exe", InstallSource.Winget)]
    [InlineData(@"C:\Program Files\WinGet\Packages\Hostpad.Hostpad_Microsoft.Winget.Source_8wekyb3d8bbwe\Hostpad.exe", InstallSource.Winget)]
    [InlineData(@"C:\WORKAREA\Applications\tools\Hostpad.exe", InstallSource.Manual)]
    [InlineData(@"C:\Users\denis\Downloads\scoopish\apps\Hostpad.exe", InstallSource.Manual)]
    [InlineData("", InstallSource.Manual)]
    public void Detect_TellsPackageManagedCopiesFromManualOnes(string path, InstallSource expected)
    {
        var environment = new Dictionary<string, string>
        {
            ["USERPROFILE"] = @"C:\Users\denis",
            ["ProgramData"] = @"C:\ProgramData",
            ["SCOOP"] = @"D:\tools\scoop",
        };

        Assert.Equal(expected, InstallKind.Detect(path, name => environment.GetValueOrDefault(name)));
    }

    [Fact]
    public void RelaunchPath_GoesThroughScoopsCurrentFolder() =>
        Assert.Equal(
            @"C:\Users\denis\scoop\apps\hostpad\current\Hostpad.exe",
            UpdateInstaller.RelaunchPath(InstallSource.Scoop, @"C:\Users\denis\scoop\apps\hostpad\1.0.5\Hostpad.exe"));

    [Fact]
    public void PackageManagerScript_QuotesThePathItRestarts()
    {
        var script = UpdateInstaller.BuildPackageManagerScript("scoop update hostpad", 1234, @"C:\it's here\Hostpad.exe");

        Assert.Contains("Wait-Process -Id 1234", script, StringComparison.Ordinal);
        Assert.Contains("scoop update hostpad", script, StringComparison.Ordinal);
        Assert.Contains(@"Start-Process -FilePath 'C:\it''s here\Hostpad.exe'", script, StringComparison.Ordinal);
    }
}
