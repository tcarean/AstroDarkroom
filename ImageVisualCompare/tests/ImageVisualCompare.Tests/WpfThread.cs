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
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Runs a test body that needs WPF objects, a window or a control, on one STA thread of the test
/// run, where the application's resources are loaded and nothing is ever shown (ADR 0032). The
/// bodies run one at a time on that thread's dispatcher.
/// </summary>
internal static class WpfThread
{
    private static readonly Lazy<Dispatcher> Started = new(Start);

    /// <summary>
    /// Runs a body on the WPF test thread; its exception fails the awaiting test.
    /// </summary>
    public static Task RunAsync(Action body) => Started.Value.InvokeAsync(body).Task;

    private static Dispatcher Start()
    {
        var started = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            try
            {
                // The application's own resources, Theme.xaml among them, for the windows and
                // controls to find. Run is never called, so StartupUri opens nothing.
                new App().InitializeComponent();
                started.SetResult(Dispatcher.CurrentDispatcher);
            }
            catch (Exception ex)
            {
                started.SetException(ex);
                return;
            }

            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF tests",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return started.Task.GetAwaiter().GetResult();
    }
}
