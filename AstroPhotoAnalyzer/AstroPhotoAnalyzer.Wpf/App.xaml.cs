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
// Imaginea poate veni din linia de comanda: un shortcut, sau "Deschide cu" din Explorer.
// Nu e pentru scripting - pentru asta exista consola.

using System.Globalization;
using System.IO;
using System.Windows;

namespace AstroPhotoAnalyzer.Wpf
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

            window.Start(FileFromArgs(e.Args));
        }

        /// <summary>
        /// Fixeaza cultura pe cea invarianta, pentru numere.
        ///
        /// Consola obtine acelasi lucru din InvariantGlobalization, dar aia nu se poate
        /// folosi aici: fara date de cultura WPF nu poate lega nimic si fereastra crapa
        /// la incarcarea XAML-ului. Raportul trece oricum prin Fmt cu cultura invarianta
        /// explicita; asta e a doua plasa, pentru orice text care n-ar trece.
        ///
        /// DefaultThreadCurrentCulture, nu doar CurrentCulture: analiza ruleaza pe un fir
        /// din pool prin Task.Run, si acela si-ar lua cultura sistemului.
        /// </summary>
        internal static void ForceInvariantNumbers()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        /// <summary>Primul argument care e un fisier existent; orice altceva se ignora tacut.</summary>
        internal static string FileFromArgs(string[] args)
        {
            if (args == null) return null;
            foreach (string a in args)
                if (!string.IsNullOrWhiteSpace(a) && File.Exists(a)) return a;
            return null;
        }
    }
}
