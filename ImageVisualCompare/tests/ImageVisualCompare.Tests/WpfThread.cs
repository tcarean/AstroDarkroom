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

namespace ImageVisualCompare.Tests;

/// <summary>
/// Runs a test body that needs WPF objects, a window or a control, on one STA thread of the test
/// run, where the application's theme is loaded into a plain application object and nothing is
/// ever shown (ADR 0032, ADR 0046). The bodies run one at a time on that thread's dispatcher.
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
                // A plain Application with the application's theme, for the windows and controls
                // to find, never the application's own App: WPF's Application constructor queues
                // the application's start, which runs at the dispatcher's first pass even though
                // Application.Run is never called, and App's would read the test host's arguments
                // for the language and open its StartupUri window (ADR 0046). The URI names the
                // assembly, since a plain application's own is the test host's. It shuts down only
                // when told to: AboutWindowTests close the windows they build, and under the
                // default, OnLastWindowClose, closing the thread's last window ends the
                // application, the next window failing with "The Application object is being
                // shut down"; the dispatcher runs on either way, since Application.Run is never
                // called.
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/ImageVisualCompare.Wpf;component/Theme.xaml"),
                });
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
