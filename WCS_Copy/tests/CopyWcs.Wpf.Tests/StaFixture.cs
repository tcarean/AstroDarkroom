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

// Copied from DarksCheck's window tests (ADR 0007).

using System.Windows;
using System.Windows.Threading;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// A real UI thread for the tests that touch windows. Three things shape it, all learned in
/// FlatCheck: STA, because WPF requires it and xunit runs on MTA threads; a Dispatcher.Run
/// message loop, not a bare thread, because the view-models continue on the dispatcher after
/// every await and would otherwise wait forever; and one per process, because Application is
/// unique and bound to the thread that made it, so the tests share it through a collection
/// fixture.
/// </summary>
public sealed class StaFixture : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher _dispatcher = null!;

    public StaFixture()
    {
        using ManualResetEventSlim ready = new();

        _thread = new Thread(() =>
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            Application app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/CopyWcs.Wpf;component/Theme/Dark.xaml"),
            });
            App.ForceInvariantNumbers();

            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "sta-tests" };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
    }

    /// <summary>Runs on the UI thread and waits; exceptions propagate as an ordinary test failure.</summary>
    public void Invoke(Action body) => _dispatcher.Invoke(body, DispatcherPriority.Normal);

    /// <summary>As <see cref="Invoke"/>, for async work: the continuations come back to this pumping thread.</summary>
    public Task InvokeAsync(Func<Task> body) => _dispatcher.InvokeAsync(body).Task.Unwrap();

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

/// <summary>The window tests share one UI thread, so they cannot run in parallel.</summary>
[CollectionDefinition("window")]
public class WindowGroup : ICollectionFixture<StaFixture>
{
}
