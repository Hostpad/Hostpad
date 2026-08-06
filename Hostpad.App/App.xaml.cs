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
        ApplyTheme(session.Settings.Theme);

        if (!Unlock(session))
        {
            Shutdown();
            return;
        }

        new MainWindow(new MainViewModel(session)).Show();
    }

    /// <summary>
    /// Opens the vault, asking for the master password when the file needs one
    /// or when the user chose to be prompted. Returns false when the user gives
    /// up, which is a cancel rather than an error.
    /// </summary>
    private static bool Unlock(VaultSession session)
    {
        var prompt = "Enter your master password.";

        while (true)
        {
            string? password = null;

            if (session.RequiresPassword || session.Settings.RequirePasswordOnStartup)
            {
                password = PasswordDialog.Ask(owner: null, prompt);

                if (password is null)
                {
                    return false;
                }
            }

            try
            {
                session.Open(password);
                return true;
            }
            catch (VaultAuthenticationException)
            {
                // Only worth retrying when a password can actually help.
                if (!session.RequiresPassword && !session.Settings.RequirePasswordOnStartup)
                {
                    ReportAndGiveUp(session, "This vault cannot be opened on this computer.");
                    return false;
                }

                prompt = "That password was not accepted. Try again.";
            }
            catch (VaultException ex)
            {
                ReportAndGiveUp(session, ex.Message);
                return false;
            }
        }
    }

    private static void ReportAndGiveUp(VaultSession session, string message) =>
        MessageBox.Show(
            $"Hostpad could not open {session.VaultPath}.\n\n{message}",
            "Hostpad",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

    public static void ApplyTheme(AppTheme theme)
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
