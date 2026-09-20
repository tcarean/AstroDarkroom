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

// StaFixture.cs - un fir de interfata adevarat, pentru testele care ating ferestre.
//
// Trei lucruri il fac sa arate asa (invatate in FlatCheck cand nu aratau asa):
//   1. STA - WPF o cere; xunit ruleaza pe fire MTA.
//   2. Dispatcher.Run(), nu doar un fir STA: ViewModel-urile isi cer continuarea inapoi pe
//      contextul de sincronizare, care pe un fir cu Application e dispecerul. Fara bucla
//      de mesaje, continuarea se pune in coada si nu se executa niciodata - deadlock.
//   3. Unul singur pe proces: Application e unic si legat de firul care l-a creat.

using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AstroPhotoAnalyzer.Wpf.Tests;

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
                Source = new Uri("pack://application:,,,/AstroPhotoAnalyzer.Wpf;component/Theme/Dark.xaml"),
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

    public void Invoke(Action body) => _dispatcher.Invoke(body, DispatcherPriority.Normal);

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

/// <summary>Testele de fereastra impart un singur fir de interfata, deci nu pot rula in paralel.</summary>
[CollectionDefinition("fereastra")]
public class WindowCollection : ICollectionFixture<StaFixture> { }
