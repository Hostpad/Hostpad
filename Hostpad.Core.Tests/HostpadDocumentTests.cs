using Hostpad.Core.Model;
using Xunit;

namespace Hostpad.Core.Tests;

public class HostpadDocumentTests
{
    [Fact]
    public void Validate_ReportsNothing_ForAConsistentDocument()
    {
        var group = new ConnectionGroup { Name = "Production" };
        var profile = new ConnectionProfile { Name = "PuTTY default", Protocol = Protocol.Ssh };
        var document = new HostpadDocument
        {
            Groups = [group],
            Profiles = [profile],
            Connections =
            [
                new Connection
                {
                    Name = "web-01",
                    Host = "10.0.0.1",
                    Protocol = Protocol.Ssh,
                    GroupId = group.Id,
                    ProfileId = profile.Id,
                },
            ],
        };

        Assert.Empty(document.Validate());
    }

    [Fact]
    public void Validate_FlagsAProfileWhoseProtocolDoesNotMatch()
    {
        var profile = new ConnectionProfile { Name = "RDP admin", Protocol = Protocol.Rdp };
        var document = new HostpadDocument
        {
            Profiles = [profile],
            Connections =
            [
                new Connection
                {
                    Name = "web-01",
                    Host = "10.0.0.1",
                    Protocol = Protocol.Ssh,
                    ProfileId = profile.Id,
                },
            ],
        };

        var problem = Assert.Single(document.Validate());
        Assert.Contains("RDP admin", problem);
    }

    [Fact]
    public void WouldCreateCycle_DetectsReparentingAGroupUnderItsOwnDescendant()
    {
        var parent = new ConnectionGroup { Name = "Customers" };
        var child = new ConnectionGroup { Name = "Acme", ParentId = parent.Id };
        var document = new HostpadDocument { Groups = [parent, child] };

        Assert.True(document.WouldCreateCycle(parent.Id, child.Id));
        Assert.False(document.WouldCreateCycle(child.Id, null));
    }

    [Fact]
    public void DuplicateAs_CopiesTheDataButNotTheIdentity()
    {
        var original = new Connection
        {
            Name = "web-01",
            Host = "10.0.0.1",
            Tags = ["prod"],
            Credential = new Credential { Username = "root", Password = "hunter2" },
        };

        var copy = original.DuplicateAs("web-02");

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("web-02", copy.Name);
        Assert.Equal("10.0.0.1", copy.Host);
        Assert.Equal("hunter2", copy.Credential.Password);

        // Collections must not be shared with the original.
        copy.Tags.Add("staging");
        Assert.Single(original.Tags);
    }

    [Fact]
    public void EffectivePort_FallsBackToTheProtocolDefault()
    {
        var rdp = new Connection { Name = "dc-01", Host = "10.0.0.9", Protocol = Protocol.Rdp };
        Assert.Equal(3389, rdp.EffectivePort);

        rdp.Port = 13389;
        Assert.Equal(13389, rdp.EffectivePort);
    }
}
