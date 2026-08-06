using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hostpad.Core.Model;
using Wpf.Ui.Controls;

namespace Hostpad.App.ViewModels;

/// <summary>Shared base for the two kinds of row the connection tree shows.</summary>
public abstract partial class TreeNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>True while the row shows a text box for renaming in place.</summary>
    [ObservableProperty]
    private bool _isEditing;

    public required Guid Id { get; init; }
}

public sealed partial class GroupNode : TreeNode
{
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>Child groups first, then connections — the order the list renders.</summary>
    public ObservableCollection<TreeNode> Children { get; } = [];

    /// <summary>Connections in this group and in every group below it.</summary>
    public int ConnectionCount =>
        Children.Sum(child => child is GroupNode group ? group.ConnectionCount : 1);

    public SymbolRegular Icon => IsExpanded ? SymbolRegular.FolderOpen24 : SymbolRegular.Folder24;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Icon));

    public void NotifyCountChanged() => OnPropertyChanged(nameof(ConnectionCount));
}

public sealed partial class ConnectionNode : TreeNode
{
    [ObservableProperty]
    private Protocol _protocol;

    [ObservableProperty]
    private bool _hasJumpHost;

    /// <summary>Icon beside the name, chosen by what the connection opens.</summary>
    public SymbolRegular Icon => Protocol switch
    {
        Protocol.Rdp => SymbolRegular.Desktop24,
        Protocol.Vnc => SymbolRegular.ShareScreenStart24,
        Protocol.Sftp or Protocol.Scp or Protocol.Ftp => SymbolRegular.FolderArrowUp24,

        // A command prompt, not a code file: this opens a terminal session.
        _ => SymbolRegular.Prompt24,
    };

    partial void OnProtocolChanged(Protocol value) => OnPropertyChanged(nameof(Icon));
}
