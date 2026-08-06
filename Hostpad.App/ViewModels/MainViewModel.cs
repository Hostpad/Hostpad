using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hostpad.App.Services;
using Hostpad.Core.Launching;
using Hostpad.Core.Model;
using Hostpad.Core.Storage;

namespace Hostpad.App.ViewModels;

/// <summary>
/// Drives the main window: builds the connection tree, tracks the selection and
/// applies edits back to the document.
/// <para>
/// The tree is rebuilt from the document after every change rather than patched
/// in place. With a few hundred connections it is imperceptible, and it removes
/// a whole class of bugs where the view and the document drift apart.
/// </para>
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly VaultSession _session;
    private readonly LaunchService _launcher;
    private readonly IProcessRunner _runner;

    /// <summary>Guards against re-entering the save prompt while it is saving.</summary>
    private bool _flushing;

    [ObservableProperty]
    private TreeNode? _selectedNode;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _groupConnections = true;

    public MainViewModel(VaultSession session, IProcessRunner? runner = null, LaunchService? launcher = null)
    {
        _session = session;
        _runner = runner ?? new ProcessRunner();
        _launcher = launcher ?? new LaunchService();
        GroupConnections = session.Settings.GroupConnections;
        RebuildTree();
        UpdateStatus();
    }

    public ObservableCollection<TreeNode> Nodes { get; } = [];

    public ConnectionEditorViewModel Editor { get; } = new();

    private HostpadDocument Document => _session.Document;

    /// <summary>
    /// Asked before unsaved edits would be lost. True saves, false discards.
    /// The window supplies it; the view model does not open dialogs itself.
    /// </summary>
    public Func<string, bool>? AskSaveChanges { get; set; }

    partial void OnSelectedNodeChanging(TreeNode? value) => FlushPendingEdits();

    partial void OnSearchTextChanging(string value) => FlushPendingEdits();

    partial void OnGroupConnectionsChanging(bool value) => FlushPendingEdits();

    /// <summary>
    /// Offers to save the form before something replaces it. Runs before the
    /// change takes effect, so the editor still holds the connection it belongs to.
    /// </summary>
    public void FlushPendingEdits()
    {
        if (_flushing || !Editor.IsDirty)
        {
            return;
        }

        if (Editor.ConnectionId is not { } id || Document.FindConnection(id) is not { } connection)
        {
            Editor.MarkClean();
            return;
        }

        _flushing = true;
        try
        {
            if (AskSaveChanges?.Invoke(connection.Name) == true && Editor.ApplyTo(connection))
            {
                _session.Save();

                // Update the row in place rather than rebuilding: a rebuild here
                // would replace the node the pending selection is about to point at.
                if (FindNode(Nodes, id) is ConnectionNode node)
                {
                    node.Name = connection.Name;
                    node.Protocol = connection.Protocol;
                    node.HasJumpHost = connection.Jump is not null;
                }

                UpdateStatus();
            }

            Editor.MarkClean();
        }
        finally
        {
            _flushing = false;
        }
    }

    partial void OnSelectedNodeChanged(TreeNode? value)
    {
        Editor.Load(value is ConnectionNode node ? Document.FindConnection(node.Id) : null);
        ConnectCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => RebuildTree();

    partial void OnGroupConnectionsChanged(bool value)
    {
        _session.Settings.GroupConnections = value;
        _session.SaveSettings();
        RebuildTree();
    }

    /// <summary>
    /// The group a new item belongs in: the selected group, or the group holding
    /// the selected connection, or the root when nothing is selected.
    /// </summary>
    private Guid? TargetGroupId => SelectedNode switch
    {
        GroupNode group => group.Id,
        ConnectionNode connection => Document.FindConnection(connection.Id)?.GroupId,
        _ => null,
    };

    [RelayCommand]
    private void NewConnection()
    {
        var connection = new Connection
        {
            Name = UniqueName("New connection", TargetGroupId),
            Host = string.Empty,
            GroupId = TargetGroupId,
        };

        Document.Connections.Add(connection);
        SaveAndRebuild(selectId: connection.Id);
        StatusText = "New connection added. Fill in the details and press apply.";
    }

    [RelayCommand]
    private void NewGroup()
    {
        var group = new ConnectionGroup
        {
            Name = UniqueGroupName("New group", TargetGroupId),
            ParentId = TargetGroupId,
        };

        Document.Groups.Add(group);
        SaveAndRebuild(selectId: group.Id);
    }

    [RelayCommand]
    private void Apply()
    {
        if (Editor.ConnectionId is not { } id || Document.FindConnection(id) is not { } connection)
        {
            return;
        }

        if (!Editor.ApplyTo(connection))
        {
            StatusText = "Name and hostname are required.";
            return;
        }

        // Before the rebuild, which reselects and would otherwise trip the
        // unsaved-changes prompt over the edits just saved.
        Editor.MarkClean();

        SaveAndRebuild(selectId: id);
        StatusText = $"Saved {connection.Name}.";
    }

    [RelayCommand(CanExecute = nameof(HasConnectionSelected))]
    private void Duplicate()
    {
        if (SelectedNode is not ConnectionNode node || Document.FindConnection(node.Id) is not { } original)
        {
            return;
        }

        var copy = original.DuplicateAs(UniqueName(original.Name, original.GroupId));
        Document.Connections.Add(copy);
        SaveAndRebuild(selectId: copy.Id);
        StatusText = $"Duplicated as {copy.Name}.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        switch (SelectedNode)
        {
            case ConnectionNode node when Document.FindConnection(node.Id) is { } connection:
                Document.Connections.Remove(connection);
                StatusText = $"Deleted {connection.Name}.";
                break;

            // Deleting a folder must not destroy what is inside it: the contents
            // move up to the root, where they stay visible and recoverable.
            case GroupNode node when Document.FindGroup(node.Id) is { } group:
                foreach (var child in Document.Connections.Where(c => c.GroupId == group.Id))
                {
                    child.GroupId = group.ParentId;
                }

                foreach (var child in Document.Groups.Where(g => g.ParentId == group.Id))
                {
                    child.ParentId = group.ParentId;
                }

                Document.Groups.Remove(group);
                StatusText = $"Deleted group {group.Name}. Its contents moved up one level.";
                break;

            default:
                return;
        }

        SaveAndRebuild(selectId: null);
    }

    [RelayCommand(CanExecute = nameof(HasConnectionSelected))]
    private void Connect() => ConnectWith(Editor.Protocol);

    [RelayCommand]
    private void ConnectWith(Protocol protocol)
    {
        if (SelectedNode is not ConnectionNode node || Document.FindConnection(node.Id) is not { } connection)
        {
            return;
        }

        try
        {
            var plan = _launcher.CreatePlan(connection, protocol, _session.Settings);
            _runner.Start(plan);

            connection.LastUsedUtc = DateTimeOffset.UtcNow;
            _session.Save();

            StatusText = $"Opened {connection.Name} with {protocol.DisplayName()}.";
        }
        catch (LaunchException ex)
        {
            // A missing tool or a blank hostname is an everyday mistake, not a
            // crash: say what went wrong and leave the selection alone.
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void ToggleGrouping() => GroupConnections = !GroupConnections;

    private bool HasSelection() => SelectedNode is not null;

    private bool HasConnectionSelected() => SelectedNode is ConnectionNode;

    /// <summary>Moves a connection into another group, or to the root when the target is null.</summary>
    public void MoveConnection(Guid connectionId, Guid? targetGroupId)
    {
        if (Document.FindConnection(connectionId) is not { } connection ||
            connection.GroupId == targetGroupId)
        {
            return;
        }

        connection.GroupId = targetGroupId;
        connection.Name = UniqueName(connection.Name, targetGroupId, exclude: connection.Id);
        connection.ModifiedUtc = DateTimeOffset.UtcNow;

        SaveAndRebuild(selectId: connectionId);
        StatusText = targetGroupId is null
            ? $"Moved {connection.Name} out of its group."
            : $"Moved {connection.Name} to {Document.FindGroup(targetGroupId.Value)?.Name}.";
    }

    /// <summary>Moves a group under another group, refusing moves that would create a cycle.</summary>
    public void MoveGroup(Guid groupId, Guid? targetParentId)
    {
        if (Document.FindGroup(groupId) is not { } group ||
            group.ParentId == targetParentId ||
            groupId == targetParentId ||
            Document.WouldCreateCycle(groupId, targetParentId))
        {
            return;
        }

        group.ParentId = targetParentId;
        SaveAndRebuild(selectId: groupId);
    }

    /// <summary>Exposed so the Options dialog can edit the same session this window uses.</summary>
    public VaultSession Session => _session;

    /// <summary>Writes a password-protected copy for sharing.</summary>
    public void ExportTo(string path, string password, bool includePasswords)
    {
        DocumentTransfer.Export(
            Document,
            path,
            password,
            new ExportOptions { IncludePasswords = includePasswords });

        StatusText = includePasswords
            ? $"Exported {Document.Connections.Count} connections with their passwords."
            : $"Exported {Document.Connections.Count} connections without passwords.";
    }

    /// <summary>Merges an AutoPuTTY list into this vault, optionally taking its tool settings too.</summary>
    public MergeResult ImportFromAutoPutty(
        string path,
        string? password,
        DuplicateHandling handling,
        bool includeToolSettings)
    {
        var result = new AutoPuttyImporter().Import(path, password);
        var merged = DocumentTransfer.Merge(Document, result.Document, handling);

        if (includeToolSettings)
        {
            AutoPuttyImporter.ApplyConfig(result.Config, _session.Settings);
            _session.SaveSettings();
        }

        SaveAndRebuild(selectId: null);

        StatusText = $"Imported from AutoPuTTY: {merged.Added} added, " +
                     $"{merged.Replaced} replaced, {merged.Skipped} skipped.";

        return merged;
    }

    /// <summary>Merges another vault into this one.</summary>
    public MergeResult ImportFrom(string path, string password, DuplicateHandling handling)
    {
        var incoming = DocumentTransfer.Import(path, password);
        var result = DocumentTransfer.Merge(Document, incoming, handling);

        SaveAndRebuild(selectId: null);
        StatusText = $"Imported {result.Added} added, {result.Replaced} replaced, {result.Skipped} skipped.";

        return result;
    }

    /// <summary>Re-reads the settings that affect the list, after Options was accepted.</summary>
    public void SettingsChanged()
    {
        GroupConnections = _session.Settings.GroupConnections;
        RefreshTree();
    }

    /// <summary>The group a connection currently lives in, or null when it sits at the root.</summary>
    public Guid? GroupOf(Guid connectionId) => Document.FindConnection(connectionId)?.GroupId;

    /// <summary>Rebuilds the tree from the document, discarding uncommitted row edits.</summary>
    public void RefreshTree()
    {
        var selectedId = SelectedNode?.Id;
        RebuildTree();

        if (selectedId is { } id)
        {
            SelectedNode = FindNode(Nodes, id);
        }
    }

    /// <summary>Commits an in-place group rename. Blank names are rejected silently.</summary>
    public void RenameGroup(Guid groupId, string name)
    {
        if (Document.FindGroup(groupId) is not { } group || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();
        if (string.Equals(group.Name, trimmed, StringComparison.Ordinal))
        {
            return;
        }

        group.Name = UniqueGroupName(trimmed, group.ParentId, exclude: groupId);
        SaveAndRebuild(selectId: groupId);
    }

    public void PersistExpansion(Guid groupId, bool isExpanded)
    {
        if (Document.FindGroup(groupId) is { } group && group.IsExpanded != isExpanded)
        {
            group.IsExpanded = isExpanded;
            _session.Save();
        }
    }

    private void SaveAndRebuild(Guid? selectId)
    {
        _session.Save();
        RebuildTree();
        UpdateStatus();

        if (selectId is { } id)
        {
            SelectedNode = FindNode(Nodes, id);
        }
    }

    private void RebuildTree()
    {
        Nodes.Clear();

        var matches = Filter(Document.Connections).ToList();

        // Searching flattens the tree: when you are hunting for one machine, the
        // folder it lives in is noise.
        if (!GroupConnections || !string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var connection in matches.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Nodes.Add(CreateNode(connection));
            }

            return;
        }

        foreach (var group in Document.ChildGroups(null))
        {
            Nodes.Add(BuildGroup(group, matches));
        }

        // Connections that belong to no group sit at the bottom, below the folders.
        foreach (var connection in matches
                     .Where(c => c.GroupId is null)
                     .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Nodes.Add(CreateNode(connection));
        }
    }

    private GroupNode BuildGroup(ConnectionGroup group, List<Connection> matches)
    {
        var node = new GroupNode
        {
            Id = group.Id,
            Name = group.Name,
            IsExpanded = group.IsExpanded,
        };

        foreach (var child in Document.ChildGroups(group.Id))
        {
            node.Children.Add(BuildGroup(child, matches));
        }

        foreach (var connection in matches
                     .Where(c => c.GroupId == group.Id)
                     .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            node.Children.Add(CreateNode(connection));
        }

        return node;
    }

    private static ConnectionNode CreateNode(Connection connection) => new()
    {
        Id = connection.Id,
        Name = connection.Name,
        Protocol = connection.Protocol,
        HasJumpHost = connection.Jump is not null,
    };

    private IEnumerable<Connection> Filter(IEnumerable<Connection> connections)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return connections;
        }

        var term = SearchText.Trim();

        return connections.Where(
            c => Contains(c.Name, term) ||
                 Contains(c.Host, term) ||
                 Contains(c.Credential.Username, term) ||
                 Contains(c.Notes, term));
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) == true;

    private static TreeNode? FindNode(IEnumerable<TreeNode> nodes, Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            if (node is GroupNode group && FindNode(group.Children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void UpdateStatus()
    {
        var total = Document.Connections.Count;
        var shown = Filter(Document.Connections).Count();

        StatusText = shown == total
            ? $"{total} connection{(total == 1 ? string.Empty : "s")}"
            : $"{shown} of {total} shown";
    }

    private string UniqueName(string name, Guid? groupId, Guid? exclude = null)
    {
        var candidate = name;
        var suffix = 2;

        while (Document.Connections.Any(
                   c => c.Id != exclude &&
                        c.GroupId == groupId &&
                        string.Equals(c.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
        {
            candidate = $"{name} ({suffix++})";
        }

        return candidate;
    }

    private string UniqueGroupName(string name, Guid? parentId, Guid? exclude = null)
    {
        var candidate = name;
        var suffix = 2;

        while (Document.Groups.Any(
                   g => g.Id != exclude &&
                        g.ParentId == parentId &&
                        string.Equals(g.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
        {
            candidate = $"{name} ({suffix++})";
        }

        return candidate;
    }
}
