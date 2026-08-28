using System.Windows.Threading;
using Microsoft.Win32;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// Unlock detection through <see cref="SystemEvents.SessionSwitch"/>.
///
/// SystemEvents raises its callbacks on a private worker thread, not on ours, so every
/// notification is marshalled onto the WPF dispatcher before it reaches the coordinator.
/// Only <see cref="SessionSwitchReason.SessionUnlock"/> is forwarded: SessionLogon would
/// pop the window during sign-in, and the remote/console connect reasons belong to RDP
/// reconnects rather than to a real unlock.
/// </summary>
internal sealed class SystemEventsSessionMonitor : ISessionMonitor
{
    private readonly Dispatcher _dispatcher;
    private bool _started;
    private bool _disposed;

    public SystemEventsSessionMonitor(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public event EventHandler? Unlocked;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        SystemEvents.SessionSwitch += OnSessionSwitch;
        _started = true;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason != SessionSwitchReason.SessionUnlock)
        {
            return;
        }

        _dispatcher.BeginInvoke(() => Unlocked?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_started)
        {
            // Leaving this subscribed keeps the process pinned by SystemEvents' own thread.
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _started = false;
        }

        _disposed = true;
    }
}
