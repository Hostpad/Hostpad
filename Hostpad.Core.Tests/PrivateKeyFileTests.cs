using Hostpad.Core.Launching;
using Hostpad.Core.Model;
using Xunit;

namespace Hostpad.Core.Tests;

public sealed class PrivateKeyFileTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "Hostpad.Keys", Guid.NewGuid().ToString("N"));

    public PrivateKeyFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string KeyFile(string firstLine)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, firstLine + "\nAAAA\n");
        return path;
    }

    [Fact]
    public void Resolve_GivesTheConnectionKeyPrecedence()
    {
        var credential = new Credential { PrivateKeyPath = @"C:\keys\one.ppk" };

        Assert.Equal(@"C:\keys\one.ppk", PrivateKeyFile.Resolve(credential, useGlobalKey: true, @"C:\keys\all.ppk"));
        Assert.Equal(@"C:\keys\one.ppk", PrivateKeyFile.Resolve(credential, useGlobalKey: false, null));
    }

    [Fact]
    public void Resolve_FallsBackToTheGlobalKeyOnlyWhenItIsSwitchedOn()
    {
        var credential = new Credential { PrivateKeyPath = "   " };

        Assert.Equal(@"C:\keys\all.ppk", PrivateKeyFile.Resolve(credential, useGlobalKey: true, @"C:\keys\all.ppk"));
        Assert.Null(PrivateKeyFile.Resolve(credential, useGlobalKey: false, @"C:\keys\all.ppk"));
        Assert.Null(PrivateKeyFile.Resolve(credential, useGlobalKey: true, " "));
    }

    [Fact]
    public void DetectFormat_RecognisesPuttyKeys()
    {
        Assert.Equal(PrivateKeyFormat.Ppk, PrivateKeyFile.DetectFormat(KeyFile("PuTTY-User-Key-File-3: ssh-ed25519")));
        Assert.Equal(PrivateKeyFormat.Ppk, PrivateKeyFile.DetectFormat(KeyFile("PuTTY-User-Key-File-2: ssh-rsa")));
    }

    [Theory]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    public void DetectFormat_RecognisesOpenSshAndPemKeys(string firstLine) =>
        Assert.Equal(PrivateKeyFormat.OpenSsh, PrivateKeyFile.DetectFormat(KeyFile(firstLine)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\does\not\exist.ppk")]
    [InlineData("<>|not a path")]
    public void DetectFormat_NeverThrowsOnWhatIsNotAKeyFile(string? path) =>
        Assert.Equal(PrivateKeyFormat.Unknown, PrivateKeyFile.DetectFormat(path));
}
