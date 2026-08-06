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

/// <summary>
/// Turns a Fluent symbol into the text FontIcon renders.
/// <para>
/// SymbolIcon casts the enum straight to a char, which silently truncates any
/// symbol above U+FFFF: Prompt24 is U+F0631 and came out as an Arabic letter.
/// Converting properly keeps the whole range usable.
/// </para>
/// </summary>
public static class Glyphs
{
    public static string Of(SymbolRegular symbol) => char.ConvertFromUtf32((int)symbol);
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

    public string Icon => Glyphs.Of(IsExpanded ? SymbolRegular.FolderOpen24 : SymbolRegular.Folder24);

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
    public string Icon => Glyphs.Of(Protocol switch
    {
        Protocol.Rdp => SymbolRegular.Desktop24,
        Protocol.Vnc => SymbolRegular.ShareScreenStart24,

        // Two arrows, not a folder: a folder glyph here reads as the new-group
        // button and the two were being confused.
        Protocol.Sftp or Protocol.Scp or Protocol.Ftp => SymbolRegular.ArrowSwap24,

        // A command prompt, not a code file: this opens a terminal session.
        _ => SymbolRegular.Prompt24,
    });

    /// <summary>Marker shown when the target is only reachable through a bastion.</summary>
    public string JumpIcon { get; } = Glyphs.Of(SymbolRegular.ArrowRouting24);

    partial void OnProtocolChanged(Protocol value) => OnPropertyChanged(nameof(Icon));
}
