using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hostpad.App.ViewModels;
using Hostpad.Core.Model;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBox = System.Windows.Controls.TextBox;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace Hostpad.App;

public partial class MainWindow : FluentWindow
{
    /// <summary>How far the pointer must travel with the button down before it counts as a drag.</summary>
    private static readonly double DragThreshold = SystemParameters.MinimumHorizontalDragDistance;

    private Point _mouseDownAt;
    private TreeNode? _mouseDownNode;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        ViewModel.SelectedNode = e.NewValue as TreeNode;

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NodeUnder(e.OriginalSource) is ConnectionNode && ViewModel.ConnectCommand.CanExecute(null))
        {
            ViewModel.ConnectCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2 when ViewModel.SelectedNode is GroupNode group:
                group.IsEditing = true;
                e.Handled = true;
                break;

            case Key.Delete when ViewModel.DeleteCommand.CanExecute(null):
                ViewModel.DeleteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter when ViewModel.ConnectCommand.CanExecute(null):
                ViewModel.ConnectCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnTreeMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownAt = e.GetPosition(null);
        _mouseDownNode = NodeUnder(e.OriginalSource);
    }

    private void OnTreeMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _mouseDownNode is null)
        {
            return;
        }

        var travelled = e.GetPosition(null) - _mouseDownAt;
        if (Math.Abs(travelled.X) < DragThreshold && Math.Abs(travelled.Y) < DragThreshold)
        {
            return;
        }

        var node = _mouseDownNode;
        _mouseDownNode = null;
        DragDrop.DoDragDrop(Tree, new DataObject(typeof(TreeNode), node), DragDropEffects.Move);
    }

    private void OnTreeDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTreeDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TreeNode)) is not TreeNode dragged)
        {
            return;
        }

        // Dropping on a group means "into that group"; dropping on a connection
        // means "beside it", so both resolve to the same target group. Empty
        // space below the tree drops to the root.
        var target = NodeUnder(e.OriginalSource);
        var targetGroupId = target switch
        {
            GroupNode group => group.Id,
            ConnectionNode connection => ViewModel.GroupOf(connection.Id),
            _ => null,
        };

        switch (dragged)
        {
            case ConnectionNode connection:
                ViewModel.MoveConnection(connection.Id, targetGroupId);
                break;

            case GroupNode group:
                ViewModel.MoveGroup(group.Id, targetGroupId == group.Id ? null : targetGroupId);
                break;
        }

        e.Handled = true;
    }

    private void OnTreeMenuOpened(object sender, RoutedEventArgs e)
    {
        TreeMenu.Items.Clear();

        if (ViewModel.SelectedNode is GroupNode)
        {
            TreeMenu.Items.Add(Item("New connection here", ViewModel.NewConnectionCommand.Execute));
            TreeMenu.Items.Add(Item("New group here", ViewModel.NewGroupCommand.Execute));
            TreeMenu.Items.Add(new Separator());
            TreeMenu.Items.Add(Item("Rename", _ => StartRename()));
            TreeMenu.Items.Add(Item("Delete group", ViewModel.DeleteCommand.Execute));
            return;
        }

        if (ViewModel.SelectedNode is not ConnectionNode)
        {
            TreeMenu.Items.Add(Item("New connection", ViewModel.NewConnectionCommand.Execute));
            TreeMenu.Items.Add(Item("New group", ViewModel.NewGroupCommand.Execute));
            return;
        }

        TreeMenu.Items.Add(Item("Connect", ViewModel.ConnectCommand.Execute));
        TreeMenu.Items.Add(new Separator());

        foreach (var protocol in ProtocolDefaults.InMenuOrder)
        {
            var captured = protocol;
            TreeMenu.Items.Add(Item(
                protocol.DisplayName(),
                _ => ViewModel.ConnectWithCommand.Execute(captured)));
        }

        TreeMenu.Items.Add(new Separator());
        TreeMenu.Items.Add(Item("Duplicate", ViewModel.DuplicateCommand.Execute));
        TreeMenu.Items.Add(Item("Delete", ViewModel.DeleteCommand.Execute));
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, IsOpen = true };

        menu.Items.Add(Item("Options", _ => ViewModel.StatusText = "Options are not built yet."));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Import…", _ => ViewModel.StatusText = "Import is not wired up yet."));
        menu.Items.Add(Item("Import from AutoPuTTY…", _ => ViewModel.StatusText = "The AutoPuTTY importer is not written yet."));
        menu.Items.Add(Item("Export…", _ => ViewModel.StatusText = "Export is not wired up yet."));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("About Hostpad", _ => ViewModel.StatusText = "Hostpad, a connection manager for Windows."));
    }

    private void StartRename()
    {
        if (ViewModel.SelectedNode is GroupNode group)
        {
            group.IsEditing = true;
        }
    }

    private static MenuItem Item(string header, Action<object?> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action(null);
        return item;
    }

    private void OnRenameBoxLoaded(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        box.Focus();
        box.SelectAll();
    }

    private void OnRenameBoxKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitRename((TextBox)sender);
                e.Handled = true;
                break;

            case Key.Escape:
                // Drop the edit without keeping what was typed.
                if (((TextBox)sender).DataContext is GroupNode group)
                {
                    group.IsEditing = false;
                    ViewModel.RefreshTree();
                }

                e.Handled = true;
                break;
        }
    }

    private void OnRenameBoxDone(object sender, RoutedEventArgs e) => CommitRename((TextBox)sender);

    private void CommitRename(TextBox box)
    {
        if (box.DataContext is not GroupNode group || !group.IsEditing)
        {
            return;
        }

        group.IsEditing = false;
        ViewModel.RenameGroup(group.Id, group.Name);
    }

    /// <summary>Walks up from the clicked element to the tree row it belongs to.</summary>
    private static TreeNode? NodeUnder(object? source)
    {
        var current = source as DependencyObject;

        while (current is not null and not TreeViewItem)
        {
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return (current as TreeViewItem)?.DataContext as TreeNode;
    }
}
