using Hostpad.Core.Model;

namespace Hostpad.Core.Launching;

/// <summary>
/// One connection attempt: which host, and which tool to reach it with.
/// <para>
/// Protocol is carried separately from the connection because the right-click
/// menu can open the same host with any tool, not only its default.
/// </para>
/// </summary>
public sealed class LaunchRequest
{
    public required Connection Connection { get; init; }

    public required Protocol Protocol { get; init; }

    public required AppSettings Settings { get; init; }

    public int Port => Connection.Port ?? Protocol.DefaultPort();
}

/// <summary>The command line a launcher wants run, before any process is started.</summary>
public sealed class LaunchPlan
{
    public required string FileName { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Temporary file the tool reads, deleted once it has been picked up.</summary>
    public string? TemporaryFile { get; init; }

    /// <summary>
    /// Arguments as a single string, for display and diagnostics. Not used to
    /// start the process: that goes through ArgumentList, which quotes properly.
    /// </summary>
    public string ArgumentsForDisplay => string.Join(' ', Arguments.Select(Quote));

    private static string Quote(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
}

public sealed class LaunchException : Exception
{
    public LaunchException(string message) : base(message)
    {
    }

    public LaunchException(string message, Exception inner) : base(message, inner)
    {
    }
}
