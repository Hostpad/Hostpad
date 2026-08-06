namespace Hostpad.Core.Model;

/// <summary>
/// A single remote target the user can launch.
/// <para>
/// Identity is <see cref="Id"/>, not <see cref="Name"/>. AutoPuTTY keyed servers
/// by name, which made renaming destructive and forbade two machines sharing a
/// label across environments. Names here are free-form labels.
/// </para>
/// </summary>
public sealed class Connection
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display label. Not unique, not an identifier.</summary>
    public required string Name { get; set; }

    public required string Host { get; set; }

    /// <summary>Null means <see cref="Protocol"/>'s default port.</summary>
    public int? Port { get; set; }

    public Protocol Protocol { get; set; } = Protocol.Ssh;

    /// <summary>Folder this connection sits in. Null means the tree root.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Cross-cutting labels, independent of the group tree. Compared case-insensitively.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Reusable tool settings applied before <see cref="Overrides"/>. Null means protocol defaults only.</summary>
    public Guid? ProfileId { get; set; }

    public Credential Credential { get; set; } = new();

    /// <summary>SSH jump host, when the target is only reachable through a bastion.</summary>
    public JumpHost? Jump { get; set; }

    /// <summary>
    /// Per-connection settings that win over the profile. Keys are the same
    /// namespaced strings used by <see cref="ConnectionProfile.Settings"/>.
    /// </summary>
    public Dictionary<string, string> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Notes { get; set; }

    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last successful launch, used for recent/most-used ordering. Null if never launched.</summary>
    public DateTimeOffset? LastUsedUtc { get; set; }

    public int EffectivePort => Port ?? Protocol.DefaultPort();

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Independent copy keeping the same identity, for export and undo.</summary>
    public Connection Clone() => new()
    {
        Id = Id,
        Name = Name,
        Host = Host,
        Port = Port,
        Protocol = Protocol,
        GroupId = GroupId,
        Tags = [.. Tags],
        ProfileId = ProfileId,
        Credential = Credential.Clone(),
        Jump = Jump?.Clone(),
        Overrides = new Dictionary<string, string>(Overrides, StringComparer.OrdinalIgnoreCase),
        Notes = Notes,
        CreatedUtc = CreatedUtc,
        ModifiedUtc = ModifiedUtc,
        LastUsedUtc = LastUsedUtc,
    };

    /// <summary>Copy carrying a fresh <see cref="Id"/>, for the duplicate action.</summary>
    public Connection DuplicateAs(string newName)
    {
        var copy = Clone();
        return new Connection
        {
            Name = newName,
            Host = copy.Host,
            Port = copy.Port,
            Protocol = copy.Protocol,
            GroupId = copy.GroupId,
            Tags = copy.Tags,
            ProfileId = copy.ProfileId,
            Credential = copy.Credential,
            Jump = copy.Jump,
            Overrides = copy.Overrides,
            Notes = copy.Notes,
        };
    }

    public override string ToString() => $"{Name} ({Protocol} {Host}:{EffectivePort})";
}
