using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Hostpad.App;

/// <summary>
/// Asks for a password: to unlock the vault at startup, or to protect an export.
/// </summary>
public partial class PasswordDialog : FluentWindow
{
    public PasswordDialog(string prompt)
    {
        InitializeComponent();
        Prompt.Text = prompt;
        Loaded += (_, _) => Entry.Focus();
    }

    public string Password => Entry.Password;

    /// <summary>Shows a message and keeps the dialog open, for a rejected password.</summary>
    public void ShowError(string message)
    {
        Error.Text = message;
        Error.Visibility = Visibility.Visible;
        Entry.Password = string.Empty;
        Entry.Focus();
    }

    /// <summary>Returns the password, or null when the user cancelled.</summary>
    public static string? Ask(Window? owner, string prompt)
    {
        var dialog = new PasswordDialog(prompt);

        if (owner is not null && owner.IsLoaded)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    private void OnOk(object sender, RoutedEventArgs e) => Accept();

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Accept();
            e.Handled = true;
        }
    }

    private void Accept()
    {
        if (!string.IsNullOrEmpty(Entry.Password))
        {
            DialogResult = true;
        }
    }
}
