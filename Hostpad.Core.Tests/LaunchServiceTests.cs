using Hostpad.Core.Launching;
using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Xunit;

namespace Hostpad.Core.Tests;

public sealed class LaunchServiceTests : IDisposable
{
    private readonly string _tools =
        Path.Combine(Path.GetTempPath(), "Hostpad.Tools", Guid.NewGuid().ToString("N"));

    private readonly List<string> _generated = [];

    public LaunchServiceTests() => Directory.CreateDirectory(_tools);

    public void Dispose()
    {
        foreach (var file in _generated)
        {
            TemporaryFiles.TryDelete(file);
        }

        if (Directory.Exists(_tools))
        {
            Directory.Delete(_tools, recursive: true);
        }
    }

    /// <summary>Creates a stand-in executable so the launcher's existence check passes.</summary>
    private string FakeTool(string name)
    {
        var path = Path.Combine(_tools, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private AppSettings SettingsWithAllTools() => new()
    {
        Putty = { Path = FakeTool("putty.exe") },
        WinScp = { Path = FakeTool("winscp.exe") },
        RemoteDesktop = { Path = FakeTool("mstsc.exe") },
        Vnc = { Path = FakeTool("vncviewer.exe") },
    };

    private static Connection Server(string host = "10.0.0.1") => new()
    {
        Name = "web-01",
        Host = host,
        Credential = new Credential { Username = "root", Password = "hunter2" },
    };

    private LaunchPlan Plan(Connection connection, Protocol protocol, AppSettings settings)
    {
        var plan = new LaunchService().CreatePlan(connection, protocol, settings);

        if (plan.TemporaryFile is { } file)
        {
            _generated.Add(file);
        }

        return plan;
    }

    [Fact]
    public void Putty_GetsHostPortUserAndPassword()
    {
        var plan = Plan(Server(), Protocol.Ssh, SettingsWithAllTools());

        Assert.EndsWith("putty.exe", plan.FileName, StringComparison.Ordinal);
        Assert.Equal(["-ssh", "-P", "22", "-l", "root", "-pw", "hunter2", "10.0.0.1"], plan.Arguments);
    }

    [Fact]
    public void Putty_UsesTheConnectionPortWhenOneIsSet()
    {
        var connection = Server();
        connection.Port = 2222;

        Assert.Contains("2222", Plan(connection, Protocol.Ssh, SettingsWithAllTools()).Arguments);
    }

    [Fact]
    public void Putty_TunnelsThroughTheJumpHost()
    {
        var connection = Server();
        connection.Jump = new JumpHost { Host = "bastion.example.com", Username = "jump", Port = 2222 };

        var plan = Plan(connection, Protocol.Ssh, SettingsWithAllTools());

        Assert.Contains("-proxycmd", plan.Arguments);
        Assert.Contains(plan.Arguments, a => a.Contains("jump@bastion.example.com", StringComparison.Ordinal));
        Assert.Contains(plan.Arguments, a => a.Contains("-P 2222", StringComparison.Ordinal));
    }

    [Fact]
    public void Putty_AddsTheKeyFileAndX11WhenConfigured()
    {
        var settings = SettingsWithAllTools();
        settings.Putty.UseKeyFile = true;
        settings.Putty.KeyFilePath = @"C:\keys\id_rsa.ppk";
        settings.Putty.X11Forwarding = true;

        var plan = Plan(Server(), Protocol.Ssh, settings);

        Assert.Contains("-i", plan.Arguments);
        Assert.Contains(@"C:\keys\id_rsa.ppk", plan.Arguments);
        Assert.Contains("-X", plan.Arguments);
    }

    [Theory]
    [InlineData(Protocol.Sftp, "sftp")]
    [InlineData(Protocol.Scp, "scp")]
    [InlineData(Protocol.Ftp, "ftp")]
    public void WinScp_UsesTheSchemeForEachTransferType(Protocol protocol, string scheme)
    {
        var plan = Plan(Server(), protocol, SettingsWithAllTools());

        Assert.EndsWith("winscp.exe", plan.FileName, StringComparison.Ordinal);
        Assert.StartsWith($"{scheme}://root:hunter2@10.0.0.1:", plan.Arguments[0], StringComparison.Ordinal);
    }

    [Fact]
    public void WinScp_SendsPassiveModeForFtpOnly()
    {
        var settings = SettingsWithAllTools();

        Assert.Contains("/passive=on", Plan(Server(), Protocol.Ftp, settings).Arguments);
        Assert.DoesNotContain(
            Plan(Server(), Protocol.Sftp, settings).Arguments,
            a => a.StartsWith("/passive", StringComparison.Ordinal));
    }

    [Fact]
    public void WinScp_EscapesCredentialsThatWouldBreakTheUrl()
    {
        var connection = Server();
        connection.Credential.Password = "p@ss:word/1";

        var url = Plan(connection, Protocol.Sftp, SettingsWithAllTools()).Arguments[0];

        Assert.DoesNotContain("p@ss:word/1", url, StringComparison.Ordinal);
        Assert.Contains("p%40ss%3Aword%2F1", url, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteDesktop_WritesAnRdpFileWithTheAddressAndUser()
    {
        var plan = Plan(Server(), Protocol.Rdp, SettingsWithAllTools());
        var contents = File.ReadAllText(plan.Arguments[0]);

        Assert.EndsWith("mstsc.exe", plan.FileName, StringComparison.Ordinal);
        Assert.Contains("full address:s:10.0.0.1:3389", contents, StringComparison.Ordinal);
        Assert.Contains("username:s:root", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoteDesktop_HonoursTheOptionsFromTheGeneralTab()
    {
        var settings = SettingsWithAllTools();
        settings.RemoteDesktop.ScreenSize = "1920x1080";
        settings.RemoteDesktop.MountLocalDrives = true;
        settings.RemoteDesktop.AdminSession = true;

        var plan = Plan(Server(), Protocol.Rdp, settings);
        var contents = File.ReadAllText(plan.Arguments[0]);

        Assert.Contains("desktopwidth:i:1920", contents, StringComparison.Ordinal);
        Assert.Contains("desktopheight:i:1080", contents, StringComparison.Ordinal);
        Assert.Contains("redirectdrives:i:1", contents, StringComparison.Ordinal);
        Assert.Contains("/admin", plan.Arguments);
    }

    [Fact]
    public void Vnc_WritesAVncFileWithTheObfuscatedPassword()
    {
        var plan = Plan(Server(), Protocol.Vnc, SettingsWithAllTools());
        var contents = File.ReadAllText(plan.Arguments[0]);

        Assert.Contains("Host=10.0.0.1", contents, StringComparison.Ordinal);
        Assert.Contains("Port=5900", contents, StringComparison.Ordinal);
        Assert.Contains("Password=", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePlan_ReportsAMissingToolInsteadOfFailingLater()
    {
        var settings = SettingsWithAllTools();
        settings.Putty.Path = @"C:\nowhere\putty.exe";

        var error = Assert.Throws<LaunchException>(
            () => new LaunchService().CreatePlan(Server(), Protocol.Ssh, settings));

        Assert.Contains("Options", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePlan_RefusesAConnectionWithNoHostname()
    {
        var connection = Server(host: string.Empty);

        var error = Assert.Throws<LaunchException>(
            () => new LaunchService().CreatePlan(connection, Protocol.Ssh, SettingsWithAllTools()));

        Assert.Contains("no hostname", error.Message, StringComparison.Ordinal);
    }
}

public class VncPasswordCipherTests
{
    /// <summary>
    /// Pins the current output so a future change cannot silently alter it.
    /// This is a regression baseline, not an externally published vector: the
    /// value was produced by this implementation and must be confirmed against
    /// a real VNC viewer before anyone treats it as authoritative.
    /// </summary>
    [Fact]
    public void Encrypt_ProducesAStableBlob()
    {
        Assert.Equal("ee5b0e48c8fe9771", VncPasswordCipher.Encrypt("1234"));
    }

    /// <summary>
    /// The bit-mirroring step is what makes VNC's DES variant work. Without it
    /// the output is still eight plausible-looking bytes, so only comparing
    /// against the unmirrored result catches its removal.
    /// </summary>
    [Fact]
    public void Encrypt_DiffersFromPlainDesWithTheUnmirroredKey()
    {
        using var des = System.Security.Cryptography.DES.Create();
        des.Key = [23, 82, 107, 6, 35, 78, 88, 7];
        des.Mode = System.Security.Cryptography.CipherMode.ECB;
        des.Padding = System.Security.Cryptography.PaddingMode.None;

        var block = new byte[8];
        System.Text.Encoding.ASCII.GetBytes("1234").CopyTo(block, 0);

        using var encryptor = des.CreateEncryptor();
        var unmirrored = Convert.ToHexString(encryptor.TransformFinalBlock(block, 0, 8)).ToLowerInvariant();

        Assert.NotEqual(unmirrored, VncPasswordCipher.Encrypt("1234"));
    }

    [Fact]
    public void Encrypt_TruncatesToTheEightCharactersVncAllows()
    {
        Assert.Equal(
            VncPasswordCipher.Encrypt("12345678"),
            VncPasswordCipher.Encrypt("123456789"));
    }

    [Fact]
    public void Encrypt_AlwaysProducesAnEightByteBlob()
    {
        Assert.Equal(16, VncPasswordCipher.Encrypt("a").Length);
    }
}
