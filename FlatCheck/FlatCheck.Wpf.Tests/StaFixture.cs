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
// TREI LUCRURI IL FAC SA ARATE ASA, si toate trei au fost invatate cand nu aratau asa:
//
//   1. STA. WPF o cere; xunit ruleaza testele pe fire MTA.
//
//   2. Dispatcher.Run(), nu doar un fir STA. Prima varianta pornea firul si il astepta cu
//      Join, iar testul se bloca definitiv: ViewModel-urile folosesc "await ...
//      ConfigureAwait(true)", adica isi cer continuarea inapoi pe contextul de sincronizare
//      - care pe un fir cu Application e dispecerul WPF. Fara bucla de mesaje, continuarea
//      se pune in coada si nu se executa niciodata, iar firul asteapta la infinit un
//      rezultat pe care singur nu-l lasa sa vina. E EXACT deadlock-ul pe care l-ar avea si
//      aplicatia daca ar bloca firul de interfata - deci bucla asta nu e un artificiu de
//      test, e ce face si aplicatia adevarata.
//
//   3. Unul singur pe proces. Application e unic, si e un DispatcherObject legat de firul
//      care l-a creat: al doilea fir care ar incerca sa-l foloseasca ar arunca. De aceea
//      firul se imparte intre teste printr-o fixtura de colectie, si nu se creeaza per test.

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
    /// Ruleaza ceva pe firul de interfata si asteapta rezultatul. Exceptiile se propaga
    /// la apelant, ca sa arate ca un esec de test obisnuit.
    /// </summary>
    public void Invoke(Action body)
        => _dispatcher.Invoke(body, DispatcherPriority.Normal);

    /// <summary>
    /// Ca <see cref="Invoke"/>, dar pentru munca asincrona: continuarile se intorc pe
    /// firul asta, care pompeaza, deci nu se blocheaza nimic.
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
/// Testele de fereastra impart un singur fir de interfata, deci nu pot rula in paralel.
/// </summary>
[CollectionDefinition("fereastra")]
public class WindowCollection : ICollectionFixture<StaFixture> { }
