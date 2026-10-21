// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Windows.Threading;
using Microsoft.Win32;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// Lock and unlock detection through <see cref="SystemEvents.SessionSwitch"/>.
///
/// SystemEvents raises its callbacks on a private worker thread, not on ours, so every
/// notification is marshalled onto the WPF dispatcher before it reaches the coordinator.
/// Only the two workstation reasons are forwarded: SessionLogon would pop the window during
/// sign-in, and the remote/console connect reasons belong to RDP reconnects rather than to a
/// real unlock.
/// </summary>
internal sealed class SystemEventsSessionMonitor : ISessionMonitor
{
    private readonly Dispatcher _dispatcher;
    private bool _started;
    private bool _disposed;

    /// <summary>A monitor that raises nothing until <see cref="Start"/>.</summary>
    /// <param name="dispatcher">The UI thread's dispatcher; both events are raised on it.</param>
    public SystemEventsSessionMonitor(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public event EventHandler? Unlocked;

    public event EventHandler? Locked;

    /// <inheritdoc/>
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

    /// <summary>
    /// Forwards a lock or an unlock to the dispatcher's thread, as <see cref="Locked"/> or
    /// <see cref="Unlocked"/>. Every other reason is ignored.
    /// </summary>
    /// <param name="sender">Not read.</param>
    /// <param name="e">Why the session switched.</param>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionUnlock:
                _dispatcher.BeginInvoke(() => Unlocked?.Invoke(this, EventArgs.Empty));
                break;

            case SessionSwitchReason.SessionLock:
                _dispatcher.BeginInvoke(() => Locked?.Invoke(this, EventArgs.Empty));
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Unsubscribes from <see cref="SystemEvents.SessionSwitch"/>; <see cref="Start"/> throws
    /// <see cref="ObjectDisposedException"/> afterwards. Calling it again does nothing.
    /// </summary>
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
