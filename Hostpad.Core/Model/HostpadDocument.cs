namespace Hostpad.Core.Model;

/// <summary>
/// Everything that gets encrypted and written as one unit: the connection tree,
/// its profiles, and the connections themselves.
/// <para>
/// Tool paths and window state are deliberately <em>not</em> here — those are
/// non-secret application settings and live in a separate plain file, so the
/// encrypted document only ever holds data worth protecting.
/// </para>
/// </summary>
public sealed class HostpadDocument
{
    /// <summary>Bumped whenever the persisted shape changes; drives migration on load.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ConnectionGroup> Groups { get; set; } = [];

    public List<ConnectionProfile> Profiles { get; set; } = [];

    public List<Connection> Connections { get; set; } = [];

    public ConnectionGroup? FindGroup(Guid id) => Groups.FirstOrDefault(g => g.Id == id);

    public ConnectionProfile? FindProfile(Guid id) => Profiles.FirstOrDefault(p => p.Id == id);

    public Connection? FindConnection(Guid id) => Connections.FirstOrDefault(c => c.Id == id);

    /// <summary>Direct children of <paramref name="parentId"/>, ordered as the UI shows them.</summary>
    public IEnumerable<ConnectionGroup> ChildGroups(Guid? parentId) =>
        Groups.Where(g => g.ParentId == parentId)
              .OrderBy(g => g.SortOrder)
              .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);

    public IEnumerable<Connection> ConnectionsIn(Guid? groupId) =>
        Connections.Where(c => c.GroupId == groupId)
                   .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Every tag in use, deduplicated case-insensitively.</summary>
    public IEnumerable<string> AllTags() =>
        Connections.SelectMany(c => c.Tags)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>
    /// True when reparenting <paramref name="groupId"/> under
    /// <paramref name="newParentId"/> would create a cycle or detach the tree.
    /// </summary>
    public bool WouldCreateCycle(Guid groupId, Guid? newParentId)
    {
        var cursor = newParentId;
        while (cursor is not null)
        {
            if (cursor == groupId)
            {
                return true;
            }

            cursor = FindGroup(cursor.Value)?.ParentId;
        }

        return false;
    }

    /// <summary>
    /// Structural problems that make the document unsafe to use: dangling
    /// references and group cycles. An empty result means the document is sound.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        foreach (var group in Groups.Where(g => g.ParentId is not null))
        {
            if (FindGroup(group.ParentId!.Value) is null)
            {
                problems.Add($"Group '{group.Name}' references a missing parent {group.ParentId}.");
            }
            else if (WouldCreateCycle(group.Id, group.ParentId))
            {
                problems.Add($"Group '{group.Name}' is part of a parent cycle.");
            }
        }

        foreach (var connection in Connections)
        {
            if (connection.GroupId is { } gid && FindGroup(gid) is null)
            {
                problems.Add($"Connection '{connection.Name}' references a missing group {gid}.");
            }

            if (connection.ProfileId is not { } pid)
            {
                continue;
            }

            var profile = FindProfile(pid);
            if (profile is null)
            {
                problems.Add($"Connection '{connection.Name}' references a missing profile {pid}.");
            }
            else if (profile.Protocol != connection.Protocol)
            {
                problems.Add(
                    $"Connection '{connection.Name}' is {connection.Protocol} but profile " +
                    $"'{profile.Name}' is {profile.Protocol}.");
            }
        }

        return problems;
    }
}
