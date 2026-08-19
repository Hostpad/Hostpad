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

        if (e.Args.Contains("--render-icon"))
        {
            RenderIcon();
            return;
        }

        if (e.Args.Contains("--demo"))
        {
            // --keep leaves the demo window open instead of capturing and
            // exiting, which is what you want when taking screenshots by hand
            // rather than regenerating the one in the readme.
            RunDemo(keepOpen: e.Args.Contains("--keep"));
            return;
        }

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

    /// <summary>
    /// Draws the application icon at every size Windows asks for. The shapes
    /// mirror docs/icon.svg, which is the source of the design; this exists
    /// because nothing in WPF can rasterise an SVG.
    /// </summary>
    private static void RenderIcon()
    {
        foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256 })
        {
            var visual = new System.Windows.Media.DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                var scale = size / 256.0;
                context.PushTransform(new System.Windows.Media.ScaleTransform(scale, scale));

                context.DrawRoundedRectangle(
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x1E, 0x62, 0xD0)),
                    null,
                    new Rect(0, 0, 256, 256),
                    56,
                    56);

                // The two posts of the H.
                foreach (var left in new[] { 72, 156 })
                {
                    context.DrawRoundedRectangle(
                        System.Windows.Media.Brushes.White,
                        null,
                        new Rect(left, 64, 28, 128),
                        14,
                        14);
                }

                // The bar linking them, which runs past both posts.
                context.DrawRoundedRectangle(
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xC5, 0x3D)),
                    null,
                    new Rect(56, 114, 144, 28),
                    14,
                    14);

                context.Pop();
            }

            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(visual);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

            using var stream = System.IO.File.Create(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"hostpad-icon-{size}.png"));
            encoder.Save(stream);
        }

        Environment.Exit(0);
    }

    /// <summary>
    /// Fills a throwaway vault with invented servers and captures the window,
    /// so the screenshot in the README never shows anyone's real machines.
    /// </summary>
    /// <param name="keepOpen">Leave the window up for hand-taken screenshots.</param>
    private static void RunDemo(bool keepOpen = false)
    {
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        // Both files are throwaway. The settings must be redirected as well as
        // the vault: they record which vault to open, so a demo that wrote into
        // the real settings would leave Hostpad opening a temporary file for
        // good, and the user staring at invented servers instead of their own.
        var scratch = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"hostpad-demo-{Guid.NewGuid():N}");

        System.IO.Directory.CreateDirectory(scratch);

        var session = new VaultSession(
            System.IO.Path.Combine(scratch, "settings.json"),
            System.IO.Path.Combine(scratch, "connections.hpx"));

        session.Open();

        var document = session.Document;
        var groups = new Dictionary<string, ConnectionGroup>();

        foreach (var name in new[] { "Acme Corp", "Northwind", "Contoso", "Home lab" })
        {
            var group = new ConnectionGroup { Name = name };
            groups[name] = group;
            document.Groups.Add(group);
        }

        void Add(string group, string name, string host, Protocol protocol, string? user, string? notes = null, bool jump = false)
        {
            var connection = new Connection
            {
                Name = name,
                Host = host,
                Protocol = protocol,
                GroupId = groups[group].Id,
                Credential = new Credential { Username = user, Password = user is null ? null : "demo" },
                Notes = notes,
            };

            if (jump)
            {
                connection.Jump = new JumpHost { Host = "bastion.acme.example", Username = "jump", Port = 2222 };
            }

            document.Connections.Add(connection);
        }

        Add("Acme Corp", "web-01", "10.20.0.11", Protocol.Ssh, "deploy", "Nginx + Docker\nCompose file in /srv/web\nCert renewal 03:00 UTC");
        Add("Acme Corp", "web-02", "10.20.0.12", Protocol.Ssh, "deploy", jump: true);
        Add("Acme Corp", "db-primary", "10.20.1.5", Protocol.Ssh, "postgres");
        Add("Acme Corp", "web files", "10.20.0.11", Protocol.Sftp, "deploy");
        Add("Acme Corp", "dc-01", "10.20.9.2", Protocol.Rdp, "administrator");

        Add("Northwind", "app-staging", "192.168.40.8", Protocol.Ssh, "ubuntu");
        Add("Northwind", "build agent", "192.168.40.9", Protocol.Ssh, "runner");
        Add("Northwind", "legacy ftp", "ftp.northwind.example", Protocol.Ftp, "transfer");

        Add("Contoso", "kiosk-lobby", "192.168.7.30", Protocol.Vnc, null);
        Add("Contoso", "terminal-server", "192.168.7.10", Protocol.Rdp, "svc-desk");

        Add("Home lab", "nas", "192.168.1.20", Protocol.Ssh, "admin");
        Add("Home lab", "router", "192.168.1.1", Protocol.Ssh, "root");

        session.Save();

        var viewModel = new MainViewModel(session);
        var window = new MainWindow(viewModel) { Width = 980, Height = 660 };

        window.ContentRendered += (_, _) =>
        {
            SelectDemoConnection(viewModel);

            if (keepOpen)
            {
                return;
            }

            window.Dispatcher.BeginInvoke(() =>
            {
                CaptureWindow(window);
                Environment.Exit(0);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };

        window.Show();
    }

    private static void SelectDemoConnection(MainViewModel viewModel)
    {
        foreach (var node in viewModel.Nodes.OfType<GroupNode>())
        {
            node.IsExpanded = node.Name is "Acme Corp" or "Northwind";

            if (node.Children.FirstOrDefault(c => c.Name == "web-01") is { } target)
            {
                viewModel.SelectedNode = target;
                target.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// Renders the window over a dark plate. The Mica backdrop is composed by
    /// the desktop rather than by WPF, so a plain render leaves it transparent.
    /// </summary>
    private static void CaptureWindow(Window window)
    {
        var width = (int)window.ActualWidth;
        var height = (int)window.ActualHeight;

        var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rendered.Render(window);

        var visual = new System.Windows.Media.DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20)),
                null,
                new Rect(0, 0, width, height));
            context.DrawImage(rendered, new Rect(0, 0, width, height));
        }

        var composed = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        composed.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(composed));

        using var stream = System.IO.File.Create(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hostpad-demo.png"));
        encoder.Save(stream);
    }

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
