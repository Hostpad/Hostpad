using System.Security.Cryptography;
using System.Text;
using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Hostpad.Core.Storage;
using Xunit;

namespace Hostpad.Core.Tests;

public sealed class AutoPuttyImporterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "Hostpad.Import", Guid.NewGuid().ToString("N"));

    public AutoPuttyImporterTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// Encrypts the way AutoPuTTY did. It lives in the tests rather than in the
    /// product because Hostpad only ever needs to read this format.
    /// </summary>
    private static string Encrypt(string value, string passphrase = LegacyAutoPuttyCipher.DefaultPassphrase)
    {
        using var tripleDes = TripleDES.Create();
        tripleDes.Key = MD5.HashData(Encoding.UTF8.GetBytes(passphrase));
        tripleDes.Mode = CipherMode.ECB;
        tripleDes.Padding = PaddingMode.PKCS7;

        var plain = Encoding.UTF8.GetBytes(value);
        using var encryptor = tripleDes.CreateEncryptor();

        return Convert.ToBase64String(encryptor.TransformFinalBlock(plain, 0, plain.Length));
    }

    private string WriteList(string serversXml, string passphrase = LegacyAutoPuttyCipher.DefaultPassphrase)
    {
        var path = Path.Combine(_directory, "autoputty.xml");
        File.WriteAllText(path, $"<?xml version=\"1.0\"?>\n<List>\n{serversXml}\n</List>");
        return path;
    }

    private string Server(
        string name,
        string host,
        string? user = null,
        string? password = null,
        string? comments = null,
        int? type = null,
        string passphrase = LegacyAutoPuttyCipher.DefaultPassphrase)
    {
        var xml = new StringBuilder($"  <Server Name=\"{name}\">\n");
        xml.Append($"    <Host>{Encrypt(host, passphrase)}</Host>\n");

        if (user is not null)
        {
            xml.Append($"    <User>{Encrypt(user, passphrase)}</User>\n");
        }

        if (password is not null)
        {
            xml.Append($"    <Password>{Encrypt(password, passphrase)}</Password>\n");
        }

        if (comments is not null)
        {
            xml.Append($"    <Comments>{Encrypt(comments, passphrase)}</Comments>\n");
        }

        if (type is not null)
        {
            xml.Append($"    <Type>{type}</Type>\n");
        }

        return xml.Append("  </Server>").ToString();
    }

    [Fact]
    public void Import_DecryptsTheFieldsWithTheBuiltInKey()
    {
        var path = WriteList(Server("web-01", "10.0.0.1", "root", "hunter2", "some notes"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Equal("web-01", connection.Name);
        Assert.Equal("10.0.0.1", connection.Host);
        Assert.Equal("root", connection.Credential.Username);
        Assert.Equal("hunter2", connection.Credential.Password);
        Assert.Equal("some notes", connection.Notes);
    }

    /// <summary>Comments only exists in some builds; its absence is normal, not an error.</summary>
    [Fact]
    public void Import_AcceptsAnEntryWithNoCommentsElement()
    {
        var path = WriteList(Server("web-01", "10.0.0.1", "root"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Null(connection.Notes);
        Assert.Equal("web-01", connection.Name);
    }

    [Fact]
    public void Import_AcceptsAnEntryWithNothingButAHost()
    {
        var path = WriteList(Server("bare", "10.0.0.1"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Null(connection.Credential.Username);
        Assert.Null(connection.Credential.Password);
        Assert.Null(connection.Notes);
    }

    [Theory]
    [InlineData(null, Protocol.Ssh)]
    [InlineData(0, Protocol.Ssh)]
    [InlineData(1, Protocol.Rdp)]
    [InlineData(2, Protocol.Vnc)]
    [InlineData(3, Protocol.Scp)]
    [InlineData(4, Protocol.Sftp)]
    [InlineData(5, Protocol.Ftp)]
    public void Import_MapsTheTypeIndex(int? type, Protocol expected)
    {
        var path = WriteList(Server("s", "10.0.0.1", type: type));

        Assert.Equal(expected, Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections).Protocol);
    }

    [Fact]
    public void Import_TurnsTheNamePrefixIntoAGroup()
    {
        var path = WriteList(
            Server("Acme: web-01", "10.0.0.1") + "\n" +
            Server("Acme: web-02", "10.0.0.2") + "\n" +
            Server("Globex: db-01", "10.0.0.3"));

        var document = new AutoPuttyImporter().Import(path).Document;

        Assert.Equal(2, document.Groups.Count);
        Assert.Equal(["web-01", "web-02"], document.ConnectionsIn(document.Groups[0].Id).Select(c => c.Name));
        Assert.Empty(document.Validate());
    }

    /// <summary>Dashes appear inside real names, so splitting on them would invent folders.</summary>
    [Fact]
    public void Import_DoesNotSplitOnSeparatorsOtherThanAColon()
    {
        var path = WriteList(Server("Spazio Informatico - PROD", "10.0.0.1"));

        var document = new AutoPuttyImporter().Import(path).Document;

        Assert.Empty(document.Groups);
        Assert.Equal("Spazio Informatico - PROD", Assert.Single(document.Connections).Name);
    }

    [Fact]
    public void Import_KeepsTheFullNameWhenGroupSplittingIsOff()
    {
        var path = WriteList(Server("Acme: web-01", "10.0.0.1"));

        var document = new AutoPuttyImporter { SplitGroupsFromNames = false }.Import(path).Document;

        Assert.Empty(document.Groups);
        Assert.Equal("Acme: web-01", Assert.Single(document.Connections).Name);
    }

    [Fact]
    public void Import_SplitsTheHostPort()
    {
        var path = WriteList(Server("s", "10.0.0.1:2222"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Equal("10.0.0.1", connection.Host);
        Assert.Equal(2222, connection.Port);
    }

    [Fact]
    public void Import_LeavesAHostThatIsNotAHostPortPairAlone()
    {
        var path = WriteList(Server("s", "weird=value@a@b.example.com"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Equal("weird=value@a@b.example.com", connection.Host);
        Assert.Null(connection.Port);
    }

    [Fact]
    public void Import_UnpacksTheJumpHostSyntax()
    {
        var path = WriteList(Server("s", "10.0.0.1", "jump@bastion.example.com:2222#root"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Equal("root", connection.Credential.Username);
        Assert.Equal("bastion.example.com", connection.Jump!.Host);
        Assert.Equal("jump", connection.Jump.Username);
        Assert.Equal(2222, connection.Jump.Port);
    }

    [Fact]
    public void Import_UnpacksAJumpHostWithoutUserOrPort()
    {
        var path = WriteList(Server("s", "10.0.0.1", "bastion.example.com#root"));

        var connection = Assert.Single(new AutoPuttyImporter().Import(path).Document.Connections);

        Assert.Equal("root", connection.Credential.Username);
        Assert.Equal("bastion.example.com", connection.Jump!.Host);
        Assert.Null(connection.Jump.Username);
        Assert.Equal(22, connection.Jump.EffectivePort);
    }

    [Fact]
    public void Import_ReadsTheConfigEntriesUnencrypted()
    {
        var path = WriteList(
            "  <Config ID=\"putty\">C:\\tools\\putty.exe</Config>\n" +
            "  <Config ID=\"puttykey\">True</Config>\n" +
            Server("s", "10.0.0.1"));

        var config = new AutoPuttyImporter().Import(path).Config;

        Assert.Equal(@"C:\tools\putty.exe", config["putty"]);
        Assert.Equal("True", config["puttykey"]);
    }

    [Fact]
    public void ApplyConfig_BringsOverThePathsAndFlags()
    {
        var settings = new AppSettings();

        AutoPuttyImporter.ApplyConfig(
            new Dictionary<string, string>
            {
                ["putty"] = @"C:\tools\putty.exe",
                ["puttykey"] = "True",
                ["puttykeyfile"] = @"C:\keys\id_rsa.ppk",
                ["winscp"] = @"C:\tools\winscp.exe",
                ["rdsize"] = "1920x1080",
            },
            settings);

        Assert.Equal(@"C:\tools\putty.exe", settings.Putty.Path);
        Assert.True(settings.Putty.UseKeyFile);
        Assert.Equal(@"C:\keys\id_rsa.ppk", settings.Putty.KeyFilePath);
        Assert.Equal(@"C:\tools\winscp.exe", settings.WinScp.Path);
        Assert.Equal("1920x1080", settings.RemoteDesktop.ScreenSize);
    }

    /// <summary>A partial config must not reset the settings it says nothing about.</summary>
    [Fact]
    public void ApplyConfig_LeavesUnmentionedSettingsAlone()
    {
        var settings = new AppSettings { Vnc = { Path = @"D:\my\vncviewer.exe" } };

        AutoPuttyImporter.ApplyConfig(
            new Dictionary<string, string> { ["putty"] = @"C:\tools\putty.exe" },
            settings);

        Assert.Equal(@"D:\my\vncviewer.exe", settings.Vnc.Path);
        Assert.Equal("mstsc.exe", settings.RemoteDesktop.Path);
    }

    [Fact]
    public void ApplyConfig_IgnoresBlankAndUnparsableValues()
    {
        var settings = new AppSettings();

        AutoPuttyImporter.ApplyConfig(
            new Dictionary<string, string> { ["putty"] = "   ", ["puttykey"] = "maybe" },
            settings);

        Assert.Equal("putty.exe", settings.Putty.Path);
        Assert.False(settings.Putty.UseKeyFile);
    }

    [Fact]
    public void Import_ReportsAWrongMasterPassword()
    {
        var path = WriteList(Server("s", "10.0.0.1", passphrase: "the-real-one"));

        var error = Assert.Throws<AutoPuttyImportException>(
            () => new AutoPuttyImporter().Import(path, "wrong"));

        Assert.Contains("master password", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_ReadsAListProtectedByAMasterPassword()
    {
        var path = WriteList(Server("s", "10.0.0.1", "root", passphrase: "the-real-one"));

        var connection = Assert.Single(
            new AutoPuttyImporter().Import(path, "the-real-one").Document.Connections);

        Assert.Equal("10.0.0.1", connection.Host);
    }

    [Fact]
    public void Import_RejectsAFileThatIsNotAnAutoPuttyList()
    {
        var path = Path.Combine(_directory, "other.xml");
        File.WriteAllText(path, "<?xml version=\"1.0\"?><Something />");

        var error = Assert.Throws<AutoPuttyImportException>(() => new AutoPuttyImporter().Import(path));

        Assert.Contains("AutoPuTTY", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RejectsAFileThatIsNotXml()
    {
        var path = Path.Combine(_directory, "broken.xml");
        File.WriteAllText(path, "definitely not xml");

        Assert.Throws<AutoPuttyImportException>(() => new AutoPuttyImporter().Import(path));
    }
}
