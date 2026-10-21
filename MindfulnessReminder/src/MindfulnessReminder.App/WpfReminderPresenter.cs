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

    /// <summary>A presenter with no window open or queued.</summary>
    /// <param name="dispatcher">The UI thread's dispatcher, which the show delay runs on.</param>
    /// <param name="showDelay">
    /// How long a reminder waits before its window opens; <c>null</c> is
    /// <see cref="DefaultShowDelay"/>.
    /// </param>
    public WpfReminderPresenter(Dispatcher dispatcher, TimeSpan? showDelay = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _showDelay = showDelay ?? DefaultShowDelay;
    }

    /// <summary>A queued show counts as showing, so a burst cannot queue two windows.</summary>
    public bool IsShowing => _window is not null || _pendingShow is not null;

    /// <summary>
    /// Queues a window that says <paramref name="message"/>, to open after the show delay.
    /// While a window is open it is raised instead, and keeps its text; while one is queued,
    /// nothing changes.
    /// </summary>
    /// <param name="message">What the reminder says, drawn as given.</param>
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

    /// <summary>Opens the queued window once the show delay is up.</summary>
    /// <param name="sender">The delay's timer; not read.</param>
    /// <param name="e">Empty; not read.</param>
    private void OnPendingShowElapsed(object? sender, EventArgs e)
    {
        var message = _pendingMessage ?? ReminderSettings.DefaultMessage;
        CancelPendingShow();

        var window = new BreakWindow(message);
        window.Closed += OnWindowClosed;
        _window = window;
        window.Show();
    }

    /// <summary>
    /// Restores the open window if it is minimized and brings it to the front. Does nothing
    /// when no window is open, which includes while one is still queued.
    /// </summary>
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

    /// <summary>
    /// Forgets the window once it has closed, so that the next reminder opens a new one.
    /// </summary>
    /// <param name="sender">The window that closed.</param>
    /// <param name="e">Empty; not read.</param>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is BreakWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _window = null;
    }

    /// <summary>
    /// Stops the show delay and forgets the queued message, when a window is queued.
    /// </summary>
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
