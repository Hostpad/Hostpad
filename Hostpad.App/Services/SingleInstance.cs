using System.Runtime.InteropServices;
using System.Threading;

namespace Hostpad.App.Services;

/// <summary>
/// Keeps Hostpad to one process per Windows session, and lets a second launch
/// bring the first one forward instead of exiting silently, which looked as if
/// the double click had done nothing.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Hostpad.SingleInstance";

    /// <summary>Set by a second launch, waited on by the first.</summary>
    private const string ActivateEventName = @"Local\Hostpad.Activate";

    /// <summary>ASFW_ANY: any process may take the foreground next.</summary>
    private const int AllowAnyProcess = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>
    /// Returns the instance when this process is the first, or null after
    /// asking the running one to come forward.
    /// </summary>
    public static SingleInstance? TryAcquire()
    {
        // Created by either side, so whichever starts first, the signal from a
        // second launch is not lost: an auto-reset event stays set until the
        // first instance gets round to waiting on it.
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirst);

        if (isFirst)
        {
            return new SingleInstance(mutex, activate);
        }

        // Windows only lets the process the user is interacting with hand the
        // foreground over. This launch is that process; the running one is not,
        // and without this it would only flash on the taskbar.
        AllowSetForegroundWindow(AllowAnyProcess);
        activate.Set();

        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Runs <paramref name="onActivate"/> on a pool thread each time a second launch asks.</summary>
    public void ListenForActivation(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);

        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate,
            (_, _) => onActivate(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Released from a different thread than the one that took it; the
            // handle closing below releases it all the same.
        }

        _mutex.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
