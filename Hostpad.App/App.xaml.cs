using System.Threading;
using System.Windows;
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

        // Follows the Windows light/dark setting until the user overrides it in AppSettings.
        ApplicationThemeManager.ApplySystemTheme();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
