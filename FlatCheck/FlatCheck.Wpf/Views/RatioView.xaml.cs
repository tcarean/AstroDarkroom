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
                // Doar un punct de plecare. Setul A e adesea in sesiunea deschisa, dar
                // setul B e prin definitie alta sedinta, deci alt folder - iar dupa prima
                // alegere dialogul retine el singur unde a fost.
                InitialDirectory = isA ? vm.InitialDirectory : null,
                // Tiparul vine din SirilSession, nu scris a doua oara aici: dialogul
                // trebuie sa propuna exact ce accepta validarea, altfel omul alege din
                // lista ceva ce i se refuza imediat dupa. Filtrul tot nu garanteaza
                // nimic - se poate oricand trece pe "Toate fisierele" - deci verificarea
                // adevarata ramane in ViewModel.
                Filter = $"Master de flat ({SirilSession.RatioPattern})|{SirilSession.RatioPattern}"
                         + "|Toate fisierele|*.*",
                CheckFileExists = true,
            };

            if (dlg.ShowDialog() == true) vm.Choose(isA, dlg.FileName);
        }
    }
}
