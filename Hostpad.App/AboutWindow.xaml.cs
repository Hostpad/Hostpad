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

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
