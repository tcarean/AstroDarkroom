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

// App.xaml.cs - pornirea aplicatiei.
//
// Un singur lucru merita explicat aici: folderul poate veni din linia de comanda. Nu ca
// sa fie "scriptabila" - pentru asta exista flatcheck.exe - ci fiindca asa se poate pune
// un shortcut pe folderul sesiunii curente, si fiindca in timpul dezvoltarii scuteste
// alegerea aceluiasi folder de cincizeci de ori.

using System;
using System.Globalization;
using System.IO;
using System.Windows;

namespace FlatCheck.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ForceInvariantNumbers();

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            window.Start(FolderFromArgs(e.Args));
        }

        /// <summary>
        /// Fixeaza cultura pe cea invarianta, pentru numere.
        ///
        /// Aplicatia de consola obtine acelasi lucru din &lt;InvariantGlobalization&gt;, dar
        /// aia nu se poate folosi aici: fara date de cultura, WPF nu poate lega nimic si
        /// fereastra crapa la incarcarea XAML-ului. Deci se face de mana, si e obligatoriu,
        /// nu cosmetic - pe un Windows romanesc raportul salvat ar iesi cu "0,24" unde
        /// consola scrie "0.24", si cerinta e ca fisierul sa fie IDENTIC cu iesirea din
        /// consola, nu doar echivalent.
        ///
        /// DefaultThreadCurrentCulture, nu doar CurrentCulture: analizoarele ruleaza pe
        /// fire din pool prin Task.Run, iar alea si-ar lua cultura sistemului.
        /// </summary>
        internal static void ForceInvariantNumbers()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Primul argument care arata a folder existent. Orice altceva se ignora tacut:
        /// o fereastra care refuza sa porneasca din cauza unui argument gresit e mai
        /// enervanta decat una care intreaba ce folder vrei.
        /// </summary>
        internal static string FolderFromArgs(string[] args)
        {
            if (args == null) return null;

            foreach (string a in args)
                if (!string.IsNullOrWhiteSpace(a) && Directory.Exists(a)) return a;

            return null;
        }
    }
}
