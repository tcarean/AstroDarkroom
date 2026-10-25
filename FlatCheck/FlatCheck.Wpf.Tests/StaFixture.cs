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

// StaFixture.cs - a real UI thread, for the tests that touch windows.
//
// THREE THINGS MAKE IT LOOK LIKE THIS, and all three were learnt when it did not:
//
//   1. STA. WPF requires it; xunit runs the tests on MTA threads.
//
//   2. Dispatcher.Run(), not just an STA thread. The first version started the thread and
//      waited for it with Join, and the test hung for good: the view-models use "await ...
//      ConfigureAwait(true)", that is, they ask for their continuation back on the
//      synchronisation context - which on a thread with an Application is the WPF
//      dispatcher. Without a message loop the continuation is queued and never runs, and
//      the thread waits forever for a result it does not itself let come. It is EXACTLY the
//      deadlock the application would have too if it blocked the UI thread - so this loop
//      is not a test artifice, it is what the real application does too.
//
//   3. Only one per process. Application is unique, and it is a DispatcherObject bound to
//      the thread that created it: a second thread that tried to use it would throw. That
//      is why the thread is shared between tests through a collection fixture, and not
//      created per test.

using System.Windows;
using System.Windows.Threading;

namespace FlatCheck.Wpf.Tests;

public sealed class StaFixture : IDisposable
{
    readonly Thread _thread;
    Dispatcher _dispatcher = null!;

    public StaFixture()
    {
        using var ready = new ManualResetEventSlim();

        _thread = new Thread(() =>
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FlatCheck.Wpf;component/Theme/Dark.xaml"),
            });
            App.ForceInvariantNumbers();

            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "sta-teste" };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
    }

    /// <summary>
    /// Runs something on the UI thread and waits for the result. Exceptions propagate to
    /// the caller, so that they show as an ordinary test failure.
    /// </summary>
    public void Invoke(Action body)
        => _dispatcher.Invoke(body, DispatcherPriority.Normal);

    /// <summary>
    /// Like <see cref="Invoke"/>, but for asynchronous work: the continuations come back to
    /// this thread, which pumps, so nothing blocks.
    /// </summary>
    public Task InvokeAsync(Func<Task> body)
        => _dispatcher.InvokeAsync(body).Task.Unwrap();

    public void Dispose()
    {
        _dispatcher.InvokeAsync(() =>
        {
            Application.Current?.Shutdown();
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        });
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}

/// <summary>
/// The window tests share a single UI thread, so they cannot run in parallel.
/// </summary>
[CollectionDefinition("fereastra")]
public class WindowCollection : ICollectionFixture<StaFixture> { }
