using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Hostpad.Core.Storage;
using Xunit;

namespace Hostpad.Core.Tests;

public sealed class DocumentTransferTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "Hostpad.Tests", Guid.NewGuid().ToString("N"));

    private string ExportPath => Path.Combine(_directory, "shared.hpx");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static HostpadDocument SampleDocument()
    {
        var customers = new ConnectionGroup { Name = "Customers" };
        var acme = new ConnectionGroup { Name = "Acme", ParentId = customers.Id };

        return new HostpadDocument
        {
            Groups = [customers, acme],
            Connections =
            [
                new Connection
                {
                    Name = "web-01",
                    Host = "10.0.0.1",
                    GroupId = acme.Id,
                    Credential = new Credential { Username = "root", Password = "hunter2" },
                },
            ],
        };
    }

    [Fact]
    public void Export_LeavesPasswordsOutByDefault()
    {
        DocumentTransfer.Export(SampleDocument(), ExportPath, "shared-secret");

        var imported = DocumentTransfer.Import(ExportPath, "shared-secret");
        var connection = Assert.Single(imported.Connections);

        Assert.Equal("root", connection.Credential.Username);
        Assert.Null(connection.Credential.Password);
    }

    [Fact]
    public void Export_CarriesPasswordsWhenAskedTo()
    {
        DocumentTransfer.Export(
            SampleDocument(),
            ExportPath,
            "shared-secret",
            new ExportOptions { IncludePasswords = true });

        var imported = DocumentTransfer.Import(ExportPath, "shared-secret");

        Assert.Equal("hunter2", Assert.Single(imported.Connections).Credential.Password);
    }

    [Fact]
    public void Export_DoesNotMutateTheSourceDocument()
    {
        var document = SampleDocument();

        DocumentTransfer.Export(document, ExportPath, "pw");

        Assert.Equal("hunter2", document.Connections[0].Credential.Password);
    }

    [Fact]
    public void Export_IsPortable_SoItNeedsThePasswordEvenOnThisMachine()
    {
        DocumentTransfer.Export(SampleDocument(), ExportPath, "shared-secret");

        Assert.Throws<VaultAuthenticationException>(() => DocumentTransfer.Import(ExportPath, "wrong"));
        Assert.Throws<VaultAuthenticationException>(() => DocumentTransfer.Import(ExportPath, password: null!));
    }

    [Fact]
    public void Export_CarriesTheWholeAncestryOfAGroup()
    {
        DocumentTransfer.Export(SampleDocument(), ExportPath, "pw");

        var imported = DocumentTransfer.Import(ExportPath, "pw");

        Assert.Equal(2, imported.Groups.Count);
        Assert.Empty(imported.Validate());
    }

    [Fact]
    public void Export_CanSelectASubsetOfConnections()
    {
        var document = SampleDocument();
        document.Connections.Add(new Connection { Name = "db-01", Host = "10.0.0.2" });

        DocumentTransfer.Export(
            document,
            ExportPath,
            "pw",
            new ExportOptions { ConnectionIds = [document.Connections[0].Id] });

        Assert.Equal("web-01", Assert.Single(DocumentTransfer.Import(ExportPath, "pw").Connections).Name);
    }

    [Fact]
    public void Merge_LandsInTheExistingGroupRatherThanBesideIt()
    {
        var target = SampleDocument();
        var source = SampleDocument();
        source.Connections[0].Name = "web-02";

        var result = DocumentTransfer.Merge(target, source, DuplicateHandling.Skip);

        Assert.Equal(new MergeResult(Added: 1, Replaced: 0, Skipped: 0), result);
        Assert.Equal(2, target.Groups.Count);
        Assert.Equal(2, target.Connections.Count);
        Assert.Empty(target.Validate());
    }

    [Fact]
    public void Merge_SkipsDuplicatesWhenAskedTo()
    {
        var target = SampleDocument();

        var result = DocumentTransfer.Merge(target, SampleDocument(), DuplicateHandling.Skip);

        Assert.Equal(new MergeResult(Added: 0, Replaced: 0, Skipped: 1), result);
        Assert.Single(target.Connections);
    }

    [Fact]
    public void Merge_ReplacesDuplicatesWhenAskedTo()
    {
        var target = SampleDocument();
        var source = SampleDocument();
        source.Connections[0].Host = "192.168.0.1";

        var result = DocumentTransfer.Merge(target, source, DuplicateHandling.Replace);

        Assert.Equal(new MergeResult(Added: 0, Replaced: 1, Skipped: 0), result);
        Assert.Equal("192.168.0.1", Assert.Single(target.Connections).Host);
    }

    [Fact]
    public void Merge_KeepsBothBySuffixingTheIncomingName()
    {
        var target = SampleDocument();

        DocumentTransfer.Merge(target, SampleDocument(), DuplicateHandling.KeepBoth);
        DocumentTransfer.Merge(target, SampleDocument(), DuplicateHandling.KeepBoth);

        Assert.Equal(
            ["web-01", "web-01 (2)", "web-01 (3)"],
            target.Connections.Select(c => c.Name).Order());
    }

    [Fact]
    public void Merge_TreatsSameNameInDifferentGroupsAsDistinct()
    {
        var target = SampleDocument();
        var source = SampleDocument();
        source.Groups.Single(g => g.Name == "Acme").Name = "Globex";

        var result = DocumentTransfer.Merge(target, source, DuplicateHandling.Skip);

        Assert.Equal(new MergeResult(Added: 1, Replaced: 0, Skipped: 0), result);
        Assert.Equal(3, target.Groups.Count);
    }
}
