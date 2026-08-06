using Hostpad.Core.Model;
using Hostpad.Core.Security;

namespace Hostpad.Core.Storage;

/// <summary>How an incoming connection that collides with an existing one is handled.</summary>
public enum DuplicateHandling
{
    /// <summary>Keep what is already there.</summary>
    Skip = 0,

    /// <summary>Overwrite the existing entry with the incoming one.</summary>
    Replace = 1,

    /// <summary>Keep both, giving the incoming one a suffixed name.</summary>
    KeepBoth = 2,
}

public sealed class ExportOptions
{
    /// <summary>
    /// Off by default: sharing a server list is common, handing over the root
    /// passwords for those servers is not. The recipient gets everything else
    /// and fills in credentials themselves.
    /// </summary>
    public bool IncludePasswords { get; init; }

    /// <summary>Export only these connections. Null or empty means the whole document.</summary>
    public IReadOnlyCollection<Guid>? ConnectionIds { get; init; }
}

public sealed record MergeResult(int Added, int Replaced, int Skipped)
{
    public int Total => Added + Replaced + Skipped;
}

/// <summary>
/// Moving connections between vaults. Exports always carry a password: a file
/// meant to be sent to someone else cannot be tied to the sender's Windows
/// account, so DPAPI is not an option here.
/// </summary>
public static class DocumentTransfer
{
    public static void Export(
        HostpadDocument document,
        string path,
        string password,
        ExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var payload = BuildExportPayload(document, options ?? new ExportOptions());
        var plaintext = HostpadJson.SerializeToUtf8Bytes(payload, HostpadJson.PayloadOptions);

        try
        {
            var envelope = Vault.Seal(plaintext, VaultProtection.PasswordOnly(password));
            AtomicFile.WriteAllBytes(path, HostpadJson.SerializeToUtf8Bytes(envelope, HostpadJson.Options));
        }
        finally
        {
            Array.Clear(plaintext);
        }
    }

    /// <exception cref="VaultAuthenticationException">Wrong password.</exception>
    /// <exception cref="VaultFormatException">Not a Hostpad export, or corrupted.</exception>
    public static HostpadDocument Import(string path, string password) =>
        new DocumentStore(path).Load(password);

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="target"/>, recreating
    /// the group tree by name so an imported "Production" lands in the existing
    /// "Production" rather than beside it.
    /// </summary>
    public static MergeResult Merge(
        HostpadDocument target,
        HostpadDocument source,
        DuplicateHandling handling)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        var groupMap = MergeGroups(target, source);
        var profileMap = MergeProfiles(target, source);

        var added = 0;
        var replaced = 0;
        var skipped = 0;

        foreach (var incoming in source.Connections)
        {
            var copy = incoming.Clone();
            copy.GroupId = incoming.GroupId is { } sourceGroupId ? groupMap[sourceGroupId] : null;
            copy.ProfileId = incoming.ProfileId is { } sourceProfileId
                ? profileMap.GetValueOrDefault(sourceProfileId)
                : null;

            var existing = target.Connections.FirstOrDefault(
                c => c.GroupId == copy.GroupId &&
                     string.Equals(c.Name, copy.Name, StringComparison.CurrentCultureIgnoreCase));

            if (existing is null)
            {
                target.Connections.Add(copy);
                added++;
                continue;
            }

            switch (handling)
            {
                case DuplicateHandling.Skip:
                    skipped++;
                    break;

                case DuplicateHandling.Replace:
                    target.Connections.Remove(existing);
                    target.Connections.Add(copy);
                    replaced++;
                    break;

                case DuplicateHandling.KeepBoth:
                    copy.Name = UniqueName(target, copy.GroupId, copy.Name);
                    target.Connections.Add(copy);
                    added++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(handling), handling, null);
            }
        }

        return new MergeResult(added, replaced, skipped);
    }

    private static HostpadDocument BuildExportPayload(HostpadDocument document, ExportOptions options)
    {
        var selected = options.ConnectionIds is { Count: > 0 } ids
            ? document.Connections.Where(c => ids.Contains(c.Id))
            : document.Connections;

        var connections = new List<Connection>();
        foreach (var connection in selected)
        {
            var copy = connection.Clone();
            if (!options.IncludePasswords)
            {
                copy.Credential.Password = null;
            }

            connections.Add(copy);
        }

        // Carry only the groups and profiles the exported connections actually reference.
        var usedGroupIds = ClosureOfGroups(document, connections);
        var usedProfileIds = connections.Select(c => c.ProfileId).OfType<Guid>().ToHashSet();

        return new HostpadDocument
        {
            Connections = connections,
            Groups = [.. document.Groups.Where(g => usedGroupIds.Contains(g.Id))],
            Profiles = [.. document.Profiles.Where(p => usedProfileIds.Contains(p.Id))],
        };
    }

    /// <summary>Group ids used by the connections, plus every ancestor, so the tree stays whole.</summary>
    private static HashSet<Guid> ClosureOfGroups(HostpadDocument document, IEnumerable<Connection> connections)
    {
        var result = new HashSet<Guid>();

        foreach (var groupId in connections.Select(c => c.GroupId).OfType<Guid>())
        {
            var cursor = (Guid?)groupId;
            while (cursor is { } id && result.Add(id))
            {
                cursor = document.FindGroup(id)?.ParentId;
            }
        }

        return result;
    }

    /// <summary>Maps each source group id to the target group id it corresponds to, creating groups as needed.</summary>
    private static Dictionary<Guid, Guid> MergeGroups(HostpadDocument target, HostpadDocument source)
    {
        var map = new Dictionary<Guid, Guid>();

        // Parents before children, so a parent is always resolvable when its child is mapped.
        foreach (var group in source.Groups.OrderBy(g => DepthOf(source, g)))
        {
            var targetParentId = group.ParentId is { } parentId ? map.GetValueOrDefault(parentId) : (Guid?)null;

            var existing = target.Groups.FirstOrDefault(
                g => g.ParentId == targetParentId &&
                     string.Equals(g.Name, group.Name, StringComparison.CurrentCultureIgnoreCase));

            if (existing is not null)
            {
                map[group.Id] = existing.Id;
                continue;
            }

            var created = new ConnectionGroup
            {
                Name = group.Name,
                ParentId = targetParentId,
                SortOrder = group.SortOrder,
                IsExpanded = group.IsExpanded,
            };

            target.Groups.Add(created);
            map[group.Id] = created.Id;
        }

        return map;
    }

    private static Dictionary<Guid, Guid> MergeProfiles(HostpadDocument target, HostpadDocument source)
    {
        var map = new Dictionary<Guid, Guid>();

        foreach (var profile in source.Profiles)
        {
            var existing = target.Profiles.FirstOrDefault(
                p => p.Protocol == profile.Protocol &&
                     string.Equals(p.Name, profile.Name, StringComparison.CurrentCultureIgnoreCase));

            if (existing is not null)
            {
                map[profile.Id] = existing.Id;
                continue;
            }

            var created = profile.Clone();
            target.Profiles.Add(created);
            map[profile.Id] = created.Id;
        }

        return map;
    }

    private static int DepthOf(HostpadDocument document, ConnectionGroup group)
    {
        var depth = 0;
        var cursor = group.ParentId;

        while (cursor is { } id && depth < document.Groups.Count)
        {
            depth++;
            cursor = document.FindGroup(id)?.ParentId;
        }

        return depth;
    }

    private static string UniqueName(HostpadDocument document, Guid? groupId, string name)
    {
        var candidate = name;
        var suffix = 2;

        while (document.Connections.Any(
                   c => c.GroupId == groupId &&
                        string.Equals(c.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
        {
            candidate = $"{name} ({suffix++})";
        }

        return candidate;
    }
}
