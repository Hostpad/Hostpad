using System.Reflection;
using System.Windows;
using Wpf.Ui.Controls;

namespace Hostpad.App;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {ReadVersion()}";
    }

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
