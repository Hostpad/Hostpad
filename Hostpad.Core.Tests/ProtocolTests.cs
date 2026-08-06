using Hostpad.Core.Model;
using Xunit;

namespace Hostpad.Core.Tests;

public class ProtocolTests
{
    public static TheoryData<Protocol> AllProtocols()
    {
        var data = new TheoryData<Protocol>();
        foreach (var protocol in Enum.GetValues<Protocol>())
        {
            data.Add(protocol);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllProtocols))]
    public void EveryProtocol_HasAPortAToolPathAndALabel(Protocol protocol)
    {
        Assert.InRange(protocol.DefaultPort(), 1, 65535);
        Assert.False(string.IsNullOrWhiteSpace(protocol.DisplayName()));
        Assert.False(string.IsNullOrWhiteSpace(new AppSettings().ToolPathFor(protocol)));
    }

    [Fact]
    public void MenuOrder_ListsEveryProtocolExactlyOnce()
    {
        Assert.Equal(
            Enum.GetValues<Protocol>().Order(),
            ProtocolDefaults.InMenuOrder.Order());
    }

    [Fact]
    public void WinScpProtocols_ShareTheSameToolPath()
    {
        var settings = new AppSettings { WinScp = { Path = @"D:\tools\winscp.exe" } };

        Assert.Equal(@"D:\tools\winscp.exe", settings.ToolPathFor(Protocol.Sftp));
        Assert.Equal(@"D:\tools\winscp.exe", settings.ToolPathFor(Protocol.Scp));
        Assert.Equal(@"D:\tools\winscp.exe", settings.ToolPathFor(Protocol.Ftp));
        Assert.NotEqual(settings.ToolPathFor(Protocol.Ssh), settings.ToolPathFor(Protocol.Ftp));
    }

    [Fact]
    public void KeyFileAndJumpHost_ApplyToTheSshFamilyOnly()
    {
        Assert.True(Protocol.Ssh.SupportsKeyFile());
        Assert.True(Protocol.Sftp.SupportsKeyFile());
        Assert.True(Protocol.Scp.SupportsKeyFile());

        Assert.False(Protocol.Ftp.SupportsKeyFile());
        Assert.False(Protocol.Rdp.SupportsJumpHost());
        Assert.False(Protocol.Vnc.SupportsJumpHost());
    }
}
