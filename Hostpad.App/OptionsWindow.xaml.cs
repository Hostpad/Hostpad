using System.Windows;
using Hostpad.App.ViewModels;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Hostpad.App;

public partial class OptionsWindow : FluentWindow
{
    public OptionsWindow(OptionsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public OptionsViewModel ViewModel { get; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Validate() is { } problem)
        {
            System.Windows.MessageBox.Show(this, problem, "Options", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ViewModel.Apply())
        {
            DialogResult = true;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnBrowsePutty(object sender, RoutedEventArgs e) =>
        ViewModel.PuttyPath = PickExecutable(ViewModel.PuttyPath) ?? ViewModel.PuttyPath;

    private void OnBrowseRdp(object sender, RoutedEventArgs e) =>
        ViewModel.RdpPath = PickExecutable(ViewModel.RdpPath) ?? ViewModel.RdpPath;

    private void OnBrowseVnc(object sender, RoutedEventArgs e) =>
        ViewModel.VncPath = PickExecutable(ViewModel.VncPath) ?? ViewModel.VncPath;

    private void OnBrowseWinScp(object sender, RoutedEventArgs e) =>
        ViewModel.WinScpPath = PickExecutable(ViewModel.WinScpPath) ?? ViewModel.WinScpPath;

    private void OnBrowsePuttyKey(object sender, RoutedEventArgs e) =>
        ViewModel.PuttyKeyFile = PickKeyFile(ViewModel.PuttyKeyFile) ?? ViewModel.PuttyKeyFile;

    private void OnBrowseWinScpKey(object sender, RoutedEventArgs e) =>
        ViewModel.WinScpKeyFile = PickKeyFile(ViewModel.WinScpKeyFile) ?? ViewModel.WinScpKeyFile;

    private void OnBrowseRdpOutput(object sender, RoutedEventArgs e) =>
        ViewModel.RdpOutputPath = PickFolder(ViewModel.RdpOutputPath) ?? ViewModel.RdpOutputPath;

    private void OnBrowseVncOutput(object sender, RoutedEventArgs e) =>
        ViewModel.VncOutputPath = PickFolder(ViewModel.VncOutputPath) ?? ViewModel.VncOutputPath;

    private string? PickExecutable(string current) => Pick(current, "Programs (*.exe)|*.exe|All files (*.*)|*.*");

    /// <summary>PuTTY and WinSCP both want a .ppk; OpenSSH keys must be converted first.</summary>
    private string? PickKeyFile(string current) => Pick(current, "PuTTY key (*.ppk)|*.ppk|All files (*.*)|*.*");

    private string? Pick(string current, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            InitialDirectory = SafeDirectory(current),
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private string? PickFolder(string current)
    {
        var dialog = new OpenFolderDialog { InitialDirectory = SafeDirectory(current) };

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    /// <summary>A configured path may be a bare file name or nonsense; neither should throw.</summary>
    private static string SafeDirectory(string current)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(current);
            return System.IO.Directory.Exists(directory) ? directory! : string.Empty;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}
