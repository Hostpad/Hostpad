namespace Hostpad.Core.Model;

/// <summary>
/// A folder in the connection tree. Groups nest through <see cref="ParentId"/>;
/// the document is responsible for rejecting cycles.
/// </summary>
public sealed class ConnectionGroup
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>Null means a top-level group.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Manual ordering among siblings. Ties fall back to name.</summary>
    public int SortOrder { get; set; }

    /// <summary>Expand/collapse state, persisted so the tree looks the same next launch.</summary>
    public bool IsExpanded { get; set; } = true;

    public override string ToString() => Name;
}
