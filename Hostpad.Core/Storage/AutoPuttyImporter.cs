using System.Xml.Linq;
using Hostpad.Core.Model;
using Hostpad.Core.Security;

namespace Hostpad.Core.Storage;

public sealed class AutoPuttyImportResult
{
    public required HostpadDocument Document { get; init; }

    /// <summary>Tool paths and options found in the file, ready to be merged into settings.</summary>
    public required IReadOnlyDictionary<string, string> Config { get; init; }

    /// <summary>Entries that could not be decrypted, by connection name.</summary>
    public required IReadOnlyList<string> Unreadable { get; init; }
}

public sealed class AutoPuttyImportException : Exception
{
    public AutoPuttyImportException(string message) : base(message)
    {
    }

    public AutoPuttyImportException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Reads an AutoPuTTY <c>autoputty.xml</c> into Hostpad's own model.
/// <para>
/// The file format is not Hostpad's to define, so this reads defensively:
/// missing elements are normal rather than errors. Comments in particular only
/// exists in some builds, and its absence simply leaves Notes empty.
/// </para>
/// </summary>
public sealed class AutoPuttyImporter
{
    /// <summary>Type is an index into this list, and is omitted entirely when it is the first one.</summary>
    private static readonly Protocol[] TypeIndex =
    [
        Protocol.Ssh,
        Protocol.Rdp,
        Protocol.Vnc,
        Protocol.Scp,
        Protocol.Sftp,
        Protocol.Ftp,
    ];

    /// <summary>
    /// Splits "Customer: Server" into a group and a name. AutoPuTTY had no
    /// folders, so users encoded them in the name; this turns that convention
    /// back into real structure.
    /// </summary>
    public bool SplitGroupsFromNames { get; init; } = true;

    /// <param name="password">The master password, when the list was protected by one.</param>
    /// <exception cref="AutoPuttyImportException">Not an AutoPuTTY list, or the password is wrong.</exception>
    public AutoPuttyImportResult Import(string path, string? password = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        XDocument xml;
        try
        {
            xml = XDocument.Load(path);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new AutoPuttyImportException($"'{path}' is not readable XML.", ex);
        }

        var root = xml.Root
                   ?? throw new AutoPuttyImportException($"'{path}' is empty.");

        if (root.Name != "List")
        {
            throw new AutoPuttyImportException(
                $"'{path}' does not look like an AutoPuTTY list; its root element is <{root.Name}>.");
        }

        var passphrase = string.IsNullOrEmpty(password)
            ? LegacyAutoPuttyCipher.DefaultPassphrase
            : password;

        var servers = root.Elements("Server").ToList();
        if (servers.Count == 0)
        {
            throw new AutoPuttyImportException($"'{path}' contains no servers.");
        }

        var document = new HostpadDocument();
        var groups = new Dictionary<string, ConnectionGroup>(StringComparer.CurrentCultureIgnoreCase);
        var unreadable = new List<string>();

        foreach (var server in servers)
        {
            var rawName = (string?)server.Attribute("Name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                continue;
            }

            var host = Decrypt(server, "Host", passphrase);
            if (host is null)
            {
                // The whole file uses one key, so a failure here means the wrong
                // password rather than one bad row.
                unreadable.Add(rawName);
                continue;
            }

            var (groupName, name) = SplitName(rawName);
            var connection = new Connection { Name = name, Host = string.Empty };

            ApplyHost(connection, host);
            ApplyUser(connection, Decrypt(server, "User", passphrase) ?? string.Empty);

            connection.Credential.Password = NullIfBlank(Decrypt(server, "Password", passphrase));
            connection.Notes = NullIfBlank(Decrypt(server, "Comments", passphrase));
            connection.Protocol = ReadProtocol(server);

            if (groupName is not null)
            {
                connection.GroupId = GetOrAddGroup(document, groups, groupName).Id;
            }

            document.Connections.Add(connection);
        }

        if (document.Connections.Count == 0 && unreadable.Count > 0)
        {
            throw new AutoPuttyImportException(
                "Nothing could be decrypted. If the list was protected by a master password, enter it.");
        }

        return new AutoPuttyImportResult
        {
            Document = document,
            Config = ReadConfig(root),
            Unreadable = unreadable,
        };
    }

    /// <summary>
    /// Copies the tool paths and options from an imported list into settings.
    /// Keys absent from the file leave the current value alone, so importing a
    /// partial config does not reset everything else to defaults.
    /// </summary>
    public static void ApplyConfig(IReadOnlyDictionary<string, string> config, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);

        SetText(config, "putty", value => settings.Putty.Path = value);
        SetText(config, "puttycommand", value => settings.Putty.Command = value);
        SetFlag(config, "puttyexecute", value => settings.Putty.ExecuteCommands = value);
        SetFlag(config, "puttykey", value => settings.Putty.UseKeyFile = value);
        SetText(config, "puttykeyfile", value => settings.Putty.KeyFilePath = value);
        SetFlag(config, "puttyforward", value => settings.Putty.X11Forwarding = value);

        SetText(config, "remotedesktop", value => settings.RemoteDesktop.Path = value);
        SetText(config, "rdfilespath", value => settings.RemoteDesktop.OutputPath = value);
        SetText(config, "rdsize", value => settings.RemoteDesktop.ScreenSize = value);
        SetFlag(config, "rdadmin", value => settings.RemoteDesktop.AdminSession = value);
        SetFlag(config, "rddrives", value => settings.RemoteDesktop.MountLocalDrives = value);
        SetFlag(config, "rdspan", value => settings.RemoteDesktop.MultipleMonitors = value);

        SetText(config, "vnc", value => settings.Vnc.Path = value);
        SetText(config, "vncfilespath", value => settings.Vnc.OutputPath = value);
        SetFlag(config, "vncfullscreen", value => settings.Vnc.FullScreen = value);
        SetFlag(config, "vncviewonly", value => settings.Vnc.ViewOnly = value);

        SetText(config, "winscp", value => settings.WinScp.Path = value);
        SetFlag(config, "winscpkey", value => settings.WinScp.UseKeyFile = value);
        SetText(config, "winscpkeyfile", value => settings.WinScp.KeyFilePath = value);
        SetFlag(config, "winscppassive", value => settings.WinScp.PassiveMode = value);

        SetFlag(config, "minimize", value => settings.MinimizeToTray = value);
        SetFlag(config, "multicolumn", value => settings.GroupConnections = value);
    }

    private static void SetText(IReadOnlyDictionary<string, string> config, string key, Action<string> apply)
    {
        if (config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            apply(value.Trim());
        }
    }

    private static void SetFlag(IReadOnlyDictionary<string, string> config, string key, Action<bool> apply)
    {
        if (config.TryGetValue(key, out var value) && bool.TryParse(value.Trim(), out var flag))
        {
            apply(flag);
        }
    }

    /// <summary>Config values were never encrypted, so they are read as they are.</summary>
    private static Dictionary<string, string> ReadConfig(XElement root)
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in root.Elements("Config"))
        {
            var id = (string?)element.Attribute("ID");
            var value = element.Value.Trim();

            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(value))
            {
                config[id] = value;
            }
        }

        return config;
    }

    /// <summary>Missing elements are expected, not exceptional; only a decryption failure returns null.</summary>
    private static string? Decrypt(XElement server, string elementName, string passphrase)
    {
        var element = server.Element(elementName);

        return element is null
            ? string.Empty
            : LegacyAutoPuttyCipher.TryDecrypt(element.Value.Trim(), passphrase);
    }

    private static Protocol ReadProtocol(XElement server)
    {
        var text = server.Element("Type")?.Value.Trim();

        // Absent means the first entry, which is how AutoPuTTY stored PuTTY.
        return int.TryParse(text, out var index) && index >= 0 && index < TypeIndex.Length
            ? TypeIndex[index]
            : Protocol.Ssh;
    }

    /// <summary>Reads "hostname[:port]", leaving the port unset when it is absent or invalid.</summary>
    private static void ApplyHost(Connection connection, string host)
    {
        var trimmed = host.Trim();
        var separator = trimmed.LastIndexOf(':');

        if (separator > 0 &&
            trimmed.IndexOf(']') < separator &&
            int.TryParse(trimmed[(separator + 1)..], out var port) &&
            port is > 0 and <= 65535)
        {
            connection.Host = trimmed[..separator];
            connection.Port = port;
            return;
        }

        connection.Host = trimmed;
    }

    /// <summary>
    /// Unpacks AutoPuTTY's jump syntax, <c>proxyuser@proxyhost[:port]#user</c>,
    /// into a real username and a real jump host.
    /// </summary>
    private static void ApplyUser(Connection connection, string user)
    {
        var trimmed = user.Trim();
        var hash = trimmed.IndexOf('#');

        if (hash < 0)
        {
            connection.Credential.Username = NullIfBlank(trimmed);
            return;
        }

        var proxy = trimmed[..hash];
        connection.Credential.Username = NullIfBlank(trimmed[(hash + 1)..]);

        var at = proxy.IndexOf('@');
        var proxyUser = at >= 0 ? proxy[..at] : null;
        var proxyHost = at >= 0 ? proxy[(at + 1)..] : proxy;

        int? proxyPort = null;
        var colon = proxyHost.LastIndexOf(':');
        if (colon > 0 && int.TryParse(proxyHost[(colon + 1)..], out var port) && port is > 0 and <= 65535)
        {
            proxyPort = port;
            proxyHost = proxyHost[..colon];
        }

        if (!string.IsNullOrWhiteSpace(proxyHost))
        {
            connection.Jump = new JumpHost
            {
                Host = proxyHost,
                Username = NullIfBlank(proxyUser),
                Port = proxyPort,
            };
        }
    }

    private (string? Group, string Name) SplitName(string rawName)
    {
        var trimmed = rawName.Trim();

        if (!SplitGroupsFromNames)
        {
            return (null, trimmed);
        }

        // Only ": " counts. Separators such as " - " appear inside real names
        // often enough that splitting on them would invent folders.
        var separator = trimmed.IndexOf(": ", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return (null, trimmed);
        }

        var group = trimmed[..separator].Trim();
        var name = trimmed[(separator + 2)..].Trim();

        return string.IsNullOrEmpty(group) || string.IsNullOrEmpty(name)
            ? (null, trimmed)
            : (group, name);
    }

    private static ConnectionGroup GetOrAddGroup(
        HostpadDocument document,
        Dictionary<string, ConnectionGroup> groups,
        string name)
    {
        if (groups.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var group = new ConnectionGroup { Name = name };
        groups[name] = group;
        document.Groups.Add(group);

        return group;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
