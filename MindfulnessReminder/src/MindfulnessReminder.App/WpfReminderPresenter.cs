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

using System.Windows;
using System.Windows.Threading;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// Owns the single <see cref="BreakWindow"/> instance.
///
/// The window is not shown the instant the unlock event arrives: the shell is still
/// restoring the desktop then, and a window created into that moment often ends up behind
/// everything else. A short delay costs nothing and makes the reminder land on top.
/// </summary>
internal sealed class WpfReminderPresenter : IReminderPresenter
{
    public static readonly TimeSpan DefaultShowDelay = TimeSpan.FromMilliseconds(600);

    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan _showDelay;

    private BreakWindow? _window;
    private DispatcherTimer? _pendingShow;

    /// <summary>What the window queued by <see cref="Show"/> is going to say, once the delay is up.</summary>
    private string? _pendingMessage;

    public WpfReminderPresenter(Dispatcher dispatcher, TimeSpan? showDelay = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _showDelay = showDelay ?? DefaultShowDelay;
    }

    /// <summary>A queued show counts as showing, so a burst cannot queue two windows.</summary>
    public bool IsShowing => _window is not null || _pendingShow is not null;

    public void Show(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (IsShowing)
        {
            // Raised as it is, with the text it already has. A window that is still in its
            // 600 ms queue keeps the message it was queued with for the same reason.
            BringToFront();
            return;
        }

        _pendingMessage = message;
        _pendingShow = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = _showDelay,
        };

        _pendingShow.Tick += OnPendingShowElapsed;
        _pendingShow.Start();
    }

    private void OnPendingShowElapsed(object? sender, EventArgs e)
    {
        var message = _pendingMessage ?? ReminderSettings.DefaultMessage;
        CancelPendingShow();

        var window = new BreakWindow(message);
        window.Closed += OnWindowClosed;
        _window = window;
        window.Show();
    }

    public void BringToFront()
    {
        if (_window is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Topmost = true;
        window.Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is BreakWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _window = null;
    }

    private void CancelPendingShow()
    {
        if (_pendingShow is not { } timer)
        {
            return;
        }

        timer.Stop();
        timer.Tick -= OnPendingShowElapsed;
        _pendingShow = null;
        _pendingMessage = null;
    }
}
