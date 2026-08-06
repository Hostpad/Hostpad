using System.Diagnostics;
using Hostpad.Core.Launching;

namespace Hostpad.App.Services;

public interface IProcessRunner
{
    void Start(LaunchPlan plan);
}

/// <summary>
/// Starts the external client and cleans up after it.
/// <para>
/// Generated .rdp and .vnc files hold credentials, so they are removed once the
/// tool has had time to read them. Waiting on process exit would not work: the
/// tools read the file at startup and may run for hours.
/// </para>
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan CleanupDelay = TimeSpan.FromSeconds(10);

    public void Start(LaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var info = new ProcessStartInfo
        {
            FileName = plan.FileName,
            UseShellExecute = false,
        };

        foreach (var argument in plan.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info)
                                ?? throw new LaunchException($"Windows did not start '{plan.FileName}'.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TemporaryFiles.TryDelete(plan.TemporaryFile);
            throw new LaunchException($"Could not start '{plan.FileName}'. {ex.Message}", ex);
        }

        ScheduleCleanup(plan.TemporaryFile);
    }

    private static void ScheduleCleanup(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _ = Task.Delay(CleanupDelay).ContinueWith(
            _ => TemporaryFiles.TryDelete(path),
            TaskScheduler.Default);
    }
}
