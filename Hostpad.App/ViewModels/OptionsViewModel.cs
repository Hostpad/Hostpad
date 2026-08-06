using CommunityToolkit.Mvvm.ComponentModel;
using Hostpad.App.Services;
using Hostpad.Core.Model;

namespace Hostpad.App.ViewModels;

/// <summary>
/// Backs the Options dialog. Edits a copy of the settings so Cancel really
/// cancels, and applies everything in one go when the user accepts.
/// </summary>
public sealed partial class OptionsViewModel : ObservableObject
{
    private readonly VaultSession _session;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private bool _rememberWindow;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _groupConnections;

    [ObservableProperty]
    private bool _useMasterPassword;

    [ObservableProperty]
    private bool _askAtStartup;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    // PuTTY
    [ObservableProperty]
    private string _puttyPath = string.Empty;

    [ObservableProperty]
    private bool _puttyExecuteCommands;

    [ObservableProperty]
    private string _puttyCommand = string.Empty;

    [ObservableProperty]
    private bool _puttyUseKeyFile;

    [ObservableProperty]
    private string _puttyKeyFile = string.Empty;

    [ObservableProperty]
    private bool _puttyX11;

    // Remote Desktop
    [ObservableProperty]
    private string _rdpPath = string.Empty;

    [ObservableProperty]
    private string _rdpOutputPath = string.Empty;

    [ObservableProperty]
    private string _rdpScreenSize = string.Empty;

    [ObservableProperty]
    private bool _rdpAdmin;

    [ObservableProperty]
    private bool _rdpMountDrives;

    [ObservableProperty]
    private bool _rdpMultiMon;

    // VNC
    [ObservableProperty]
    private string _vncPath = string.Empty;

    [ObservableProperty]
    private string _vncOutputPath = string.Empty;

    [ObservableProperty]
    private bool _vncFullScreen;

    [ObservableProperty]
    private bool _vncViewOnly;

    // WinSCP
    [ObservableProperty]
    private string _winScpPath = string.Empty;

    [ObservableProperty]
    private bool _winScpUseKeyFile;

    [ObservableProperty]
    private string _winScpKeyFile = string.Empty;

    [ObservableProperty]
    private bool _winScpPassive;

    public OptionsViewModel(VaultSession session)
    {
        _session = session;

        var settings = session.Settings;

        Theme = settings.Theme;
        RememberWindow = settings.Window.Width is not null;
        MinimizeToTray = settings.MinimizeToTray;
        GroupConnections = settings.GroupConnections;

        UseMasterPassword = session.HasMasterPassword;
        AskAtStartup = settings.RequirePasswordOnStartup;

        PuttyPath = settings.Putty.Path;
        PuttyExecuteCommands = settings.Putty.ExecuteCommands;
        PuttyCommand = settings.Putty.Command ?? string.Empty;
        PuttyUseKeyFile = settings.Putty.UseKeyFile;
        PuttyKeyFile = settings.Putty.KeyFilePath ?? string.Empty;
        PuttyX11 = settings.Putty.X11Forwarding;

        RdpPath = settings.RemoteDesktop.Path;
        RdpOutputPath = settings.RemoteDesktop.OutputPath ?? string.Empty;
        RdpScreenSize = settings.RemoteDesktop.ScreenSize ?? string.Empty;
        RdpAdmin = settings.RemoteDesktop.AdminSession;
        RdpMountDrives = settings.RemoteDesktop.MountLocalDrives;
        RdpMultiMon = settings.RemoteDesktop.MultipleMonitors;

        VncPath = settings.Vnc.Path;
        VncOutputPath = settings.Vnc.OutputPath ?? string.Empty;
        VncFullScreen = settings.Vnc.FullScreen;
        VncViewOnly = settings.Vnc.ViewOnly;

        WinScpPath = settings.WinScp.Path;
        WinScpUseKeyFile = settings.WinScp.UseKeyFile;
        WinScpKeyFile = settings.WinScp.KeyFilePath ?? string.Empty;
        WinScpPassive = settings.WinScp.PassiveMode;
    }

    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    /// <summary>Plain-language statement of how the file is protected, shown under the options.</summary>
    public string ProtectionSummary => UseMasterPassword
        ? "This file opens on this computer without a password, and elsewhere with the master password."
        : "This file opens only on this computer. A backup restored elsewhere cannot be read.";

    public string VaultPath => _session.VaultPath;

    partial void OnUseMasterPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(ProtectionSummary));

        if (!value)
        {
            AskAtStartup = false;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }
    }

    /// <summary>Explains why the settings cannot be applied, or null when they can.</summary>
    public string? Validate()
    {
        if (!UseMasterPassword)
        {
            return null;
        }

        var changingPassword = !_session.HasMasterPassword || !string.IsNullOrEmpty(Password);

        if (!changingPassword)
        {
            return null;
        }

        if (string.IsNullOrEmpty(Password))
        {
            return "Enter a master password, or turn the option off.";
        }

        return Password == ConfirmPassword ? null : "The two passwords do not match.";
    }

    /// <summary>Writes the form back into the session. Returns false when validation fails.</summary>
    public bool Apply()
    {
        if (Validate() is not null)
        {
            return false;
        }

        var settings = _session.Settings;

        settings.Theme = Theme;
        settings.MinimizeToTray = MinimizeToTray;
        settings.GroupConnections = GroupConnections;
        settings.RequirePasswordOnStartup = UseMasterPassword && AskAtStartup;

        settings.Putty.Path = PuttyPath.Trim();
        settings.Putty.ExecuteCommands = PuttyExecuteCommands;
        settings.Putty.Command = NullIfBlank(PuttyCommand);
        settings.Putty.UseKeyFile = PuttyUseKeyFile;
        settings.Putty.KeyFilePath = NullIfBlank(PuttyKeyFile);
        settings.Putty.X11Forwarding = PuttyX11;

        settings.RemoteDesktop.Path = RdpPath.Trim();
        settings.RemoteDesktop.OutputPath = NullIfBlank(RdpOutputPath);
        settings.RemoteDesktop.ScreenSize = NullIfBlank(RdpScreenSize);
        settings.RemoteDesktop.AdminSession = RdpAdmin;
        settings.RemoteDesktop.MountLocalDrives = RdpMountDrives;
        settings.RemoteDesktop.MultipleMonitors = RdpMultiMon;

        settings.Vnc.Path = VncPath.Trim();
        settings.Vnc.OutputPath = NullIfBlank(VncOutputPath);
        settings.Vnc.FullScreen = VncFullScreen;
        settings.Vnc.ViewOnly = VncViewOnly;

        settings.WinScp.Path = WinScpPath.Trim();
        settings.WinScp.UseKeyFile = WinScpUseKeyFile;
        settings.WinScp.KeyFilePath = NullIfBlank(WinScpKeyFile);
        settings.WinScp.PassiveMode = WinScpPassive;

        // Rewrapping is cheap but not free, so only touch it when it changed.
        if (!UseMasterPassword && _session.HasMasterPassword)
        {
            _session.SetMasterPassword(null);
        }
        else if (UseMasterPassword && !string.IsNullOrEmpty(Password))
        {
            _session.SetMasterPassword(Password);
        }

        _session.SaveSettings();
        return true;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
