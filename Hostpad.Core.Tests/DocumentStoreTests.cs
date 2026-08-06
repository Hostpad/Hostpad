using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Hostpad.Core.Storage;
using Xunit;

namespace Hostpad.Core.Tests;

public sealed class DocumentStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "Hostpad.Tests", Guid.NewGuid().ToString("N"));

    private string VaultPath => Path.Combine(_directory, "connections.hpx");

    private static VaultProtection PasswordOnly(string password) =>
        new() { UseDpapi = false, Password = password, Iterations = 1_000 };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static HostpadDocument SampleDocument(out Guid groupId)
    {
        var group = new ConnectionGroup { Name = "Production" };
        groupId = group.Id;

        return new HostpadDocument
        {
            Groups = [group],
            Connections =
            [
                new Connection
                {
                    Name = "web-01",
                    Host = "10.0.0.1",
                    Protocol = Protocol.Ssh,
                    GroupId = group.Id,
                    Tags = ["prod", "web"],
                    Credential = new Credential { Username = "root", Password = "hunter2" },
                    Jump = new JumpHost { Host = "bastion.example.com", Username = "jump", Port = 2222 },
                },
            ],
        };
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheWholeDocument()
    {
        var store = new DocumentStore(VaultPath);
        var document = SampleDocument(out var groupId);

        store.Save(document, PasswordOnly("pw"));
        var loaded = store.Load("pw");

        var connection = Assert.Single(loaded.Connections);
        Assert.Equal("web-01", connection.Name);
        Assert.Equal(groupId, connection.GroupId);
        Assert.Equal(["prod", "web"], connection.Tags);
        Assert.Equal("hunter2", connection.Credential.Password);
        Assert.Equal("bastion.example.com", connection.Jump!.Host);
        Assert.Equal(2222, connection.Jump.Port);
        Assert.Empty(loaded.Validate());
    }

    [Fact]
    public void SavedFile_ContainsNoPlaintextSecrets()
    {
        var store = new DocumentStore(VaultPath);
        store.Save(SampleDocument(out _), PasswordOnly("pw"));

        var onDisk = File.ReadAllText(VaultPath);

        Assert.DoesNotContain("hunter2", onDisk, StringComparison.Ordinal);
        Assert.DoesNotContain("web-01", onDisk, StringComparison.Ordinal);
        Assert.DoesNotContain("bastion.example.com", onDisk, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsTheWrongPassword()
    {
        var store = new DocumentStore(VaultPath);
        store.Save(SampleDocument(out _), PasswordOnly("right"));

        Assert.Throws<VaultAuthenticationException>(() => store.Load("wrong"));
    }

    [Fact]
    public void Load_ReportsAFileThatIsNotAVault()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(VaultPath, "this is not a vault");

        Assert.Throws<VaultFormatException>(() => new DocumentStore(VaultPath).Load("pw"));
    }

    [Fact]
    public void ChangeProtection_SwitchesThePasswordWithoutLosingData()
    {
        var store = new DocumentStore(VaultPath);
        store.Save(SampleDocument(out _), PasswordOnly("old"));

        store.ChangeProtection("old", PasswordOnly("new"));

        Assert.Single(store.Load("new").Connections);
        Assert.Throws<VaultAuthenticationException>(() => store.Load("old"));
    }

    [Fact]
    public void Save_OverAnExistingVault_KeepsABackupOfThePreviousVersion()
    {
        var store = new DocumentStore(VaultPath);
        store.Save(SampleDocument(out _), PasswordOnly("pw"));
        store.Save(new HostpadDocument(), PasswordOnly("pw"));

        Assert.True(File.Exists(VaultPath + ".bak"));
        Assert.False(File.Exists(VaultPath + ".tmp"));
        Assert.Empty(store.Load("pw").Connections);
    }

    [Fact]
    public void Load_FailsClearlyWhenThereIsNoVaultYet()
    {
        var store = new DocumentStore(VaultPath);

        Assert.False(store.Exists);
        Assert.Throws<FileNotFoundException>(() => store.Load("pw"));
    }
}
