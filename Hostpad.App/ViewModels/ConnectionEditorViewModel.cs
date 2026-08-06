using CommunityToolkit.Mvvm.ComponentModel;
using Hostpad.Core.Model;

namespace Hostpad.App.ViewModels;

/// <summary>
/// The right-hand form. Holds a working copy of the selected connection so
/// typing does not touch the document until Apply is pressed.
/// <para>
/// The password sits here in plaintext while the window is open, which is
/// unavoidable for a field the user edits. It is never logged or written
/// anywhere except back into the encrypted document.
/// </para>
/// </summary>
public sealed partial class ConnectionEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private Protocol _protocol = Protocol.Ssh;

    [ObservableProperty]
    private bool _hasConnection;

    public IReadOnlyList<Protocol> Protocols { get; } = ProtocolDefaults.InMenuOrder;

    public Guid? ConnectionId { get; private set; }

    /// <summary>
    /// What was loaded, so edits can be detected by comparison. A flag set from
    /// property changes would also fire when the form is being populated, and
    /// would ask about changes the user never made.
    /// </summary>
    private (string Name, string Host, string Username, string Password, string Notes, Protocol Protocol) _loaded;

    public bool IsDirty =>
        HasConnection &&
        (Name != _loaded.Name ||
         Host != _loaded.Host ||
         Username != _loaded.Username ||
         Password != _loaded.Password ||
         Notes != _loaded.Notes ||
         Protocol != _loaded.Protocol);

    public void Load(Connection? connection)
    {
        ConnectionId = connection?.Id;
        HasConnection = connection is not null;

        Name = connection?.Name ?? string.Empty;
        Host = FormatHost(connection);
        Username = connection?.Credential.Username ?? string.Empty;
        Password = connection?.Credential.Password ?? string.Empty;
        Notes = connection?.Notes ?? string.Empty;
        Protocol = connection?.Protocol ?? Protocol.Ssh;

        MarkClean();
    }

    /// <summary>Treats the current contents as saved, after applying or discarding.</summary>
    public void MarkClean() => _loaded = (Name, Host, Username, Password, Notes, Protocol);

    /// <summary>Writes the form back onto the connection. Returns false when the form is not valid.</summary>
    public bool ApplyTo(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Host))
        {
            return false;
        }

        var (host, port) = ParseHost(Host);

        connection.Name = Name.Trim();
        connection.Host = host;
        connection.Port = port;
        connection.Protocol = Protocol;
        connection.Credential.Username = NullIfBlank(Username);
        connection.Credential.Password = NullIfBlank(Password);
        connection.Notes = NullIfBlank(Notes);
        connection.ModifiedUtc = DateTimeOffset.UtcNow;

        return true;
    }

    /// <summary>Shows "host:port" only when the port is not the protocol's default.</summary>
    private static string FormatHost(Connection? connection) => connection switch
    {
        null => string.Empty,
        { Port: null } => connection.Host,
        _ => $"{connection.Host}:{connection.Port}",
    };

    /// <summary>
    /// Splits the AutoPuTTY-style "hostname[:port]" field. IPv6 literals are
    /// bracketed, so only a colon outside brackets separates the port.
    /// </summary>
    private static (string Host, int? Port) ParseHost(string text)
    {
        var trimmed = text.Trim();
        var separator = trimmed.LastIndexOf(':');

        if (separator <= 0 || trimmed.IndexOf(']') > separator)
        {
            return (trimmed, null);
        }

        var portText = trimmed[(separator + 1)..];
        return int.TryParse(portText, out var port) && port is > 0 and <= 65535
            ? (trimmed[..separator], port)
            : (trimmed, null);
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
