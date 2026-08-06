using Hostpad.Core.Model;

namespace Hostpad.Core.Launching;

/// <summary>
/// Picks the launcher for a protocol and produces the command to run. Starting
/// the process is left to the caller, which keeps this side testable and lets
/// the UI decide how to report failures.
/// </summary>
public sealed class LaunchService
{
    private readonly IReadOnlyList<ILauncher> _launchers;

    public LaunchService(IEnumerable<ILauncher>? launchers = null)
    {
        _launchers = launchers?.ToArray() ??
        [
            new PuttyLauncher(),
            new WinScpLauncher(),
            new RemoteDesktopLauncher(),
            new VncLauncher(),
        ];
    }

    /// <exception cref="LaunchException">No launcher handles the protocol, or the tool is missing.</exception>
    public LaunchPlan CreatePlan(Connection connection, Protocol protocol, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(connection.Host))
        {
            throw new LaunchException($"'{connection.Name}' has no hostname.");
        }

        var launcher = _launchers.FirstOrDefault(l => l.Handles(protocol))
                       ?? throw new LaunchException($"Nothing knows how to open {protocol.DisplayName()}.");

        var plan = launcher.CreatePlan(new LaunchRequest
        {
            Connection = connection,
            Protocol = protocol,
            Settings = settings,
        });

        if (!ToolExists(plan.FileName))
        {
            throw new LaunchException(
                $"{protocol.DisplayName()} needs '{plan.FileName}', which was not found. " +
                "Set its location in Options.");
        }

        return plan;
    }

    /// <summary>
    /// A bare file name is resolved the way the shell would: next to Hostpad
    /// first, then along PATH. That is what makes "putty.exe" work when the
    /// tools sit beside the executable.
    /// </summary>
    private static bool ToolExists(string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName);
        }

        if (File.Exists(Path.Combine(AppContext.BaseDirectory, fileName)))
        {
            return true;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        return pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => SafeExists(directory, fileName));
    }

    private static bool SafeExists(string directory, string fileName)
    {
        try
        {
            return File.Exists(Path.Combine(directory.Trim(), fileName));
        }
        catch (ArgumentException)
        {
            // PATH entries with invalid characters are not worth crashing over.
            return false;
        }
    }
}
