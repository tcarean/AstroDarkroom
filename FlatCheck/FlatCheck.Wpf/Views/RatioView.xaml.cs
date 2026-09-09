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

// RatioView.xaml.cs - legatura cu XAML-ul, plus cele doua dialoge de fisier.
//
// Dialogul se deschide de aici si nu din ViewModel dinadins: un ViewModel care deschide
// ferestre nu se mai poate testa. Aici se alege o cale, atat; validarea ei - inclusiv
// explicatia cand nu e un master de flat - e in RatioTabViewModel.Choose, unde se poate
// verifica cu un test.

using System.Windows;
using System.Windows.Controls;

namespace FlatCheck.Wpf
{
    public partial class RatioView : UserControl
    {
        public RatioView() => InitializeComponent();

        void ChooseA(object sender, RoutedEventArgs e) => Choose(isA: true);

        void ChooseB(object sender, RoutedEventArgs e) => Choose(isA: false);

        void Choose(bool isA)
        {
            if (DataContext is not RatioTabViewModel vm) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = isA ? "Masterul setului A" : "Masterul setului B",
                // Filtrul propune ce trebuie, dar nu poate garanta nimic: omul poate
                // oricand alege "Toate fisierele". Verificarea adevarata e in ViewModel.
                Filter = "Master de flat (pp_flat*.fit)|pp_flat*.fit|Toate fisierele|*.*",
                CheckFileExists = true,
            };

            if (dlg.ShowDialog() == true) vm.Choose(isA, dlg.FileName);
        }
    }
}
