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
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// The one timer, as a DispatcherTimer.
///
/// Dispatcher-based on purpose: its ticks arrive on the UI thread, which is where the
/// reminder window has to be created and where the tray tooltip has to be written, so
/// nothing here needs marshalling. It also means the timer keeps ticking while a modeless
/// settings window is open.
///
/// A DispatcherTimer does not tick while the machine sleeps, which is exactly why the
/// schedule behind it works off a deadline rather than counting these ticks.
/// </summary>
internal sealed class DispatcherPeriodicTimer : IPeriodicTimer
{
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    /// <summary>
    /// A stopped timer; once started, it ticks on the thread of <paramref name="dispatcher"/>.
    /// </summary>
    /// <param name="dispatcher">The dispatcher whose thread receives the ticks.</param>
    /// <param name="interval">The time between ticks.</param>
    public DispatcherPeriodicTimer(Dispatcher dispatcher, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
        {
            Interval = interval,
        };

        _timer.Tick += OnTick;
    }

    public event EventHandler? Tick;

    public bool IsRunning => _timer.IsEnabled;

    /// <summary>Guarded so that re-starting a running timer cannot restart its period.</summary>
    public void Start()
    {
        if (!_disposed && !_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    /// <summary>Raises <see cref="Tick"/> for each tick of the inner timer.</summary>
    /// <param name="sender">The inner timer; not read.</param>
    /// <param name="e">Empty; not read.</param>
    private void OnTick(object? sender, EventArgs e) => Tick?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Stops the timer and lets go of it; <see cref="Start"/> does nothing afterwards. Calling
    /// it again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // A DispatcherTimer left running is rooted by the dispatcher and keeps ticking into
        // a torn-down coordinator, so stopping it is the whole job here.
        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }
}
