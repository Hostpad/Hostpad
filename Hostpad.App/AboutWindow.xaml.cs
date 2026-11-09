using System.Globalization;
using System.Reflection;
using System.Windows;
using Hostpad.App.Services.Updates;
using Wpf.Ui.Controls;

namespace Hostpad.App;

public partial class AboutWindow : FluentWindow
{
    private ReleaseInfo? _release;

    /// <param name="knownRelease">A newer release the startup check already found, shown without asking again.</param>
    public AboutWindow(ReleaseInfo? knownRelease = null)
    {
        InitializeComponent();
        VersionText.Text = $"Version {ReadVersion()}";

        if (knownRelease is not null)
        {
            ShowResult(knownRelease);
        }
    }

    private async void OnCheckForUpdates(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        InstallButton.Visibility = Visibility.Collapsed;
        ReleaseLink.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = "Checking…";

        var release = await UpdateService.GetLatestReleaseAsync();

        CheckButton.IsEnabled = true;

        if (release is null)
        {
            UpdateStatus.Text = "Could not reach GitHub. Check the connection, or look at the release page.";
            return;
        }

        ShowResult(release);
    }

    private void ShowResult(ReleaseInfo release)
    {
        _release = release;
        ReleaseLink.NavigateUri = release.PageUrl.ToString();

        if (release.Version <= UpdateService.CurrentVersion)
        {
            UpdateStatus.Text = $"Hostpad is up to date. {release.Version.ToString(3)} is the latest release.";
            InstallButton.Visibility = Visibility.Collapsed;
            ReleaseLink.Visibility = Visibility.Collapsed;
            return;
        }

        ReleaseLink.Visibility = Visibility.Visible;
        InstallButton.Visibility = Visibility.Visible;

        var plan = UpdatePlan.For(release);
        UpdateStatus.Text = $"Hostpad {release.Version.ToString(3)} is available. {plan.Explanation}";
        InstallButton.Content = plan.ButtonText;
    }

    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (_release is null)
        {
            return;
        }

        var plan = UpdatePlan.For(_release);

        if (plan.Kind == UpdatePlanKind.OpenPage)
        {
            OpenInBrowser(_release.PageUrl);
            return;
        }

        if (System.Windows.MessageBox.Show(
                this,
                plan.Confirmation,
                "Update Hostpad",
                System.Windows.MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != System.Windows.MessageBoxResult.OK)
        {
            return;
        }

        if (plan.Kind == UpdatePlanKind.PackageManager)
        {
            try
            {
                CloseMainWindow();
                UpdateInstaller.StartPackageManagerUpdate(plan.Source);
            }
            catch (Exception ex) when (ex is UpdateException or System.ComponentModel.Win32Exception)
            {
                ReportFailure(ex.Message);
                return;
            }

            Application.Current.Shutdown();
            return;
        }

        SetBusy(true);
        var progress = new Progress<double>(value =>
        {
            UpdateProgress.Value = value;
            UpdateStatus.Text = string.Create(CultureInfo.CurrentCulture, $"Downloading… {value:P0}");
        });

        try
        {
            await UpdateInstaller.InstallAsync(_release, progress);
        }
        catch (UpdateException ex)
        {
            SetBusy(false);
            UpdateStatus.Text = ex.Message;
            return;
        }

        // The files are already replaced. Saving what is open comes before the
        // new process starts, so nothing is waiting on a question meanwhile.
        CloseMainWindow();

        try
        {
            UpdateInstaller.StartUpdatedInstance();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            ReportFailure($"Hostpad was updated but could not restart itself. Start it again by hand.\n\n{ex.Message}");
        }

        Application.Current.Shutdown();
    }

    private void SetBusy(bool busy)
    {
        CheckButton.IsEnabled = !busy;
        InstallButton.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
        UpdateProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Value = 0;
    }

    /// <summary>Lets the main window save the form and its position before the process goes.</summary>
    private static void CloseMainWindow() =>
        Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.Close();

    private void ReportFailure(string message) =>
        System.Windows.MessageBox.Show(
            this,
            message,
            "Update Hostpad",
            System.Windows.MessageBoxButton.OK,
            MessageBoxImage.Warning);

    private static void OpenInBrowser(Uri page) =>
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(page.ToString()) { UseShellExecute = true })?.Dispose();

    /// <summary>
    /// The informational version carries the build metadata the SDK adds, such
    /// as "1.0.0+abc1234"; only the part before the plus is worth showing.
    /// </summary>
    private static string ReadVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        }

        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
    }

    /// <summary>
    /// Opens the licence notices of the bundled libraries. They are embedded in
    /// the assembly rather than shipped beside it, because two of the three
    /// downloads are a single file with nothing next to them. Written out as
    /// .txt so every machine has something that opens it.
    /// </summary>
    private void OnShowNotices(object sender, RoutedEventArgs e)
    {
        try
        {
            using var resource = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("THIRD-PARTY-NOTICES.md")
                ?? throw new InvalidOperationException("They are missing from this build.");

            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "Hostpad-third-party-notices.txt");

            using (var file = System.IO.File.Create(path))
            {
                resource.CopyTo(file);
            }

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Hostpad could not open the third-party notices.\n\n{ex.Message}",
                "Hostpad",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
