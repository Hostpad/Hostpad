namespace Hostpad.Core.Model;

/// <summary>
/// Named AppTheme rather than ThemeMode because WPF ships its own
/// System.Windows.ThemeMode, and the two would collide in every view file.
/// </summary>
public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>
/// Non-secret application preferences, stored as plain JSON next to the
/// encrypted document. Tool paths and window geometry are not worth protecting,
/// and keeping them outside the vault means the app can start, show its window,
/// and report a sensible error before the master password is entered.
/// <para>
/// Tool settings are global, matching how AutoPuTTY worked and how the Options
/// dialog is laid out: one tab per tool, applying to every connection. Per-connection
/// variation is left to <see cref="ConnectionProfile"/>, which stays unused until a
/// real need for it turns up.
/// </para>
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public PuttySettings Putty { get; set; } = new();

    public RemoteDesktopSettings RemoteDesktop { get; set; } = new();

    public VncSettings Vnc { get; set; } = new();

    public WinScpSettings WinScp { get; set; } = new();

    public WindowState Window { get; set; } = new();

    /// <summary>Absolute path to the encrypted document. Null means the default location.</summary>
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Ask for the master password before showing the window. When false the
    /// vault is still encrypted — it is simply unlocked through the Windows
    /// account instead of a password. See VaultProtection.
    /// </summary>
    public bool RequirePasswordOnStartup { get; set; }

    /// <summary>Show the list grouped by folder rather than as one flat list.</summary>
    public bool GroupConnections { get; set; } = true;

    public string ToolPathFor(Protocol protocol) => protocol switch
    {
        Protocol.Ssh => Putty.Path,
        Protocol.Rdp => RemoteDesktop.Path,
        Protocol.Vnc => Vnc.Path,
        Protocol.Sftp or Protocol.Scp or Protocol.Ftp => WinScp.Path,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
    };
}

/// <summary>
/// PuTTY. Bare file names are resolved through PATH and the application
/// directory, which is what makes the defaults work when the tools sit next to
/// the executable.
/// </summary>
public sealed class PuttySettings
{
    public string Path { get; set; } = "putty.exe";

    /// <summary>Run <see cref="Command"/> after login.</summary>
    public bool ExecuteCommands { get; set; }

    public string? Command { get; set; }

    public bool UseKeyFile { get; set; }

    /// <summary>PuTTY needs a .ppk; OpenSSH keys have to be converted first.</summary>
    public string? KeyFilePath { get; set; }

    public bool X11Forwarding { get; set; }
}

public sealed class RemoteDesktopSettings
{
    public string Path { get; set; } = "mstsc.exe";

    /// <summary>Where generated .rdp files are written. Null means the temp directory.</summary>
    public string? OutputPath { get; set; }

    /// <summary>Free-form "1920x1080". Null or empty means mstsc's own default.</summary>
    public string? ScreenSize { get; set; }

    public bool AdminSession { get; set; }

    public bool MountLocalDrives { get; set; }

    public bool MultipleMonitors { get; set; }
}

public sealed class VncSettings
{
    public string Path { get; set; } = "vncviewer.exe";

    /// <summary>Where generated .vnc files are written. Null means the temp directory.</summary>
    public string? OutputPath { get; set; }

    public bool FullScreen { get; set; }

    public bool ViewOnly { get; set; }
}

/// <summary>WinSCP, shared by the SFTP, SCP and FTP connection types.</summary>
public sealed class WinScpSettings
{
    public string Path { get; set; } = "winscp.exe";

    public bool UseKeyFile { get; set; }

    public string? KeyFilePath { get; set; }

    /// <summary>Passive mode applies to plain FTP only.</summary>
    public bool PassiveMode { get; set; } = true;
}

public sealed class WindowState
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public bool IsMaximized { get; set; }

    /// <summary>Width of the connection list pane, in device-independent pixels.</summary>
    public double ListPaneWidth { get; set; } = 320;
}
