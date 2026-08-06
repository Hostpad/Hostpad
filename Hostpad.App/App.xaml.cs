using System.Threading;
using System.Windows;
using Hostpad.App.Services;
using Hostpad.App.ViewModels;
using Hostpad.Core.Model;
using Hostpad.Core.Security;
using Wpf.Ui.Appearance;

namespace Hostpad.App;

public partial class App : Application
{
    /// <summary>Named so a second launch can detect the first and hand focus over.</summary>
    private const string InstanceMutexName = @"Local\Hostpad.SingleInstance";

    private static Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            // TODO: signal the running instance to restore itself before exiting.
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var session = new VaultSession();

        try
        {
            session.Open();
        }
        catch (VaultException ex)
        {
            // TODO: prompt for the master password instead of giving up once
            // the Options dialog and the unlock prompt exist.
            MessageBox.Show(
                $"Hostpad could not open {session.VaultPath}.\n\n{ex.Message}",
                "Hostpad",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Shutdown();
            return;
        }

        ApplyTheme(session.Settings.Theme);

        new MainWindow(new MainViewModel(session)).Show();
    }

    private static void ApplyTheme(AppTheme theme)
    {
        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;

            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;

            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
