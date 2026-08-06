namespace Hostpad.Core.Model;

public enum ThemeMode
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
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public ToolPaths Tools { get; set; } = new();

    public WindowState Window { get; set; } = new();

    /// <summary>Absolute path to the encrypted document. Null means the default location.</summary>
    public string? DocumentPath { get; set; }

    /// <summary>Hide to the notification area instead of the taskbar when minimized.</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Ask for the master password before showing the window.</summary>
    public bool RequirePasswordOnStartup { get; set; }
}

/// <summary>
/// Where the external clients live. Bare file names are resolved through PATH
/// and the application directory, which is what makes the defaults work when the
/// tools sit next to the executable.
/// </summary>
public sealed class ToolPaths
{
    public string Ssh { get; set; } = "putty.exe";

    public string FileTransfer { get; set; } = "winscp.exe";

    public string Rdp { get; set; } = "mstsc.exe";

    public string Vnc { get; set; } = "vncviewer.exe";
}

public sealed class WindowState
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public bool IsMaximized { get; set; }

    /// <summary>Width of the connection tree pane, in device-independent pixels.</summary>
    public double TreePaneWidth { get; set; } = 260;
}
