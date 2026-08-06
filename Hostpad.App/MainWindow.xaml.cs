using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hostpad.App.ViewModels;
using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Hostpad.Core.Storage;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using IOException = System.IO.IOException;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
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

        viewModel.AskSaveChanges = AskSaveChanges;

        InitializeComponent();
        RestoreWindowState();
    }

    /// <summary>
    /// Puts the window back where it was. A saved position is only honoured if
    /// it still lands on a visible screen: monitors get unplugged, and a window
    /// restored onto one that is gone is a window the user cannot reach.
    /// </summary>
    private void RestoreWindowState()
    {
        var state = ViewModel.Session.Settings.Window;

        if (state.Width is > 0 and { } width && state.Height is > 0 and { } height)
        {
            Width = width;
            Height = height;
        }

        if (state.Left is { } left && state.Top is { } top && IsOnScreen(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (state.IsMaximized)
        {
            WindowState = System.Windows.WindowState.Maximized;
        }

        if (state.ListPaneWidth > 0)
        {
            ListColumn.Width = new GridLength(state.ListPaneWidth);
        }
    }

    private static bool IsOnScreen(double left, double top)
    {
        // A margin, so a window nudged slightly off the edge still counts.
        const double Margin = 64;

        var screen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        screen.Inflate(-Margin, -Margin);

        return screen.Contains(left, top);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing is the last chance to keep what is in the form.
        ViewModel.FlushPendingEdits();
        SaveWindowState();

        base.OnClosing(e);
    }

    private void SaveWindowState()
    {
        var state = ViewModel.Session.Settings.Window;

        // RestoreBounds holds where the window sits when it is not maximized,
        // which is what should come back after a restore.
        var bounds = WindowState == System.Windows.WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        if (bounds is { Width: > 0, Height: > 0 })
        {
            state.Left = bounds.Left;
            state.Top = bounds.Top;
            state.Width = bounds.Width;
            state.Height = bounds.Height;
        }

        state.IsMaximized = WindowState == System.Windows.WindowState.Maximized;

        if (ListColumn.ActualWidth > 0)
        {
            state.ListPaneWidth = ListColumn.ActualWidth;
        }

        ViewModel.Session.SaveSettings();
    }

    /// <summary>
    /// Asked when the form has edits that something is about to replace. There
    /// is no cancel: whatever the user does next still happens, they only choose
    /// whether the edits survive it.
    /// </summary>
    private bool AskSaveChanges(string connectionName) =>
        MessageBox.Show(
            this,
            $"Save the changes to {connectionName}?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

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

        menu.Items.Add(Item("Options", _ => ShowOptions()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Import…", _ => Import()));
        menu.Items.Add(Item("Import from AutoPuTTY…", _ => ImportFromAutoPutty()));
        menu.Items.Add(Item("Export…", _ => Export()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("About Hostpad", _ => new AboutWindow { Owner = this }.ShowDialog()));
    }

    private void ShowOptions()
    {
        var dialog = new OptionsWindow(new OptionsViewModel(ViewModel.Session)) { Owner = this };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        App.ApplyTheme(ViewModel.Session.Settings.Theme);
        ViewModel.SettingsChanged();
        ViewModel.StatusText = "Options saved.";
    }

    private void Export()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export connections",
            Filter = "Hostpad vault (*.hpx)|*.hpx",
            FileName = "connections.hpx",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        // An export always carries a password: it is meant to leave this machine,
        // so it cannot rely on the Windows account that protects the local vault.
        var password = PasswordDialog.Ask(this, "Choose a password for the exported file. Whoever opens it will need this password.");
        if (password is null)
        {
            return;
        }

        var includePasswords = MessageBox.Show(
            this,
            "Include the saved passwords for each connection?\n\n" +
            "Choose No to share the server list without handing over credentials.",
            "Export",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        try
        {
            ViewModel.ExportTo(dialog.FileName, password, includePasswords);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportFromAutoPutty()
    {
        var suggested = AppPaths.FindLegacyAutoPuttyFile();

        var dialog = new OpenFileDialog
        {
            Title = "Import from AutoPuTTY",
            Filter = "AutoPuTTY list (autoputty.xml)|autoputty.xml|XML files (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true,
            FileName = suggested ?? "autoputty.xml",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (AskDuplicateHandling() is not { } handling)
        {
            return;
        }

        var includeSettings = MessageBox.Show(
            this,
            "Also take the tool paths and options from the AutoPuTTY file?\n\n" +
            "This overwrites the matching settings in Options.",
            "Import from AutoPuTTY",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        // Most lists use AutoPuTTY's built-in key, so try without a password
        // first and only ask when that turns out to be wrong.
        string? password = null;

        while (true)
        {
            try
            {
                ViewModel.ImportFromAutoPutty(dialog.FileName, password, handling, includeSettings);
                return;
            }
            catch (AutoPuttyImportException ex)
                when (ex.Message.Contains("master password", StringComparison.OrdinalIgnoreCase))
            {
                password = PasswordDialog.Ask(
                    this,
                    password is null
                        ? "This list is protected by an AutoPuTTY master password. Enter it."
                        : "That password was not accepted. Try again.");

                if (password is null)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is AutoPuttyImportException or IOException)
            {
                MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
    }

    /// <summary>Asks how to treat names that already exist. Null means the user cancelled.</summary>
    private DuplicateHandling? AskDuplicateHandling()
    {
        var choice = MessageBox.Show(
            this,
            "Replace connections that already exist with the same name?\n\n" +
            "Yes replaces them, No keeps what is already here.",
            "Import",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return choice switch
        {
            MessageBoxResult.Yes => DuplicateHandling.Replace,
            MessageBoxResult.No => DuplicateHandling.Skip,
            _ => null,
        };
    }

    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import connections",
            Filter = "Hostpad vault (*.hpx)|*.hpx|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (AskDuplicateHandling() is not { } handling)
        {
            return;
        }

        var password = PasswordDialog.Ask(this, "Enter the password for this file.");

        if (password is null)
        {
            return;
        }

        try
        {
            ViewModel.ImportFrom(dialog.FileName, password, handling);
        }
        catch (VaultException ex)
        {
            MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
