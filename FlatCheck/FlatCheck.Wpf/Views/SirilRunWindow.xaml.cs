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

// SirilRunWindow.xaml.cs - fereastra de rulare a unui script Siril.
//
// Tot ce e stare sta in SirilRunViewModel. Aici raman lucrurile care chiar au nevoie de
// o fereastra: pornirea la incarcare, derularea log-ului la coada, confirmarea la
// inchidere in timpul rularii, si bara de titlu intunecata.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FlatCheck.Wpf
{
    public partial class SirilRunWindow : Window
    {
        readonly SirilRunViewModel _vm;
        readonly SirilRunner _runner;

        internal SirilRunWindow(SirilRunViewModel vm, SirilRunner runner)
        {
            InitializeComponent();
            _vm = vm;
            _runner = runner;
            DataContext = vm;

            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            Loaded += (_, _) => _ = _vm.RunAsync(_runner);
            Closing += OnClosing;
        }

        /// <summary>
        /// Inchiderea in timpul rularii inseamna oprirea lui Siril la mijloc, deci se
        /// intreaba. "Nu" lasa fereastra deschisa; "Da" omoara procesul si inchide -
        /// verdictul de anulare ajunge in ViewModel, pe care nu-l mai vede nimeni.
        /// </summary>
        void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_vm.IsRunning) return;

            var answer = MessageBox.Show(this,
                "Scriptul inca ruleaza. Il opresc? Fisierele scrise pe jumatate raman in process/ "
                + "si o rulare noua le suprascrie.",
                _vm.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes) { e.Cancel = true; return; }
            _vm.Cancel();
        }

        void CancelRun(object sender, RoutedEventArgs e) => _vm.Cancel();

        void CloseWindow(object sender, RoutedEventArgs e) => Close();

        /// <summary>Log-ul urmareste ultima linie, ca panoul din Siril.</summary>
        void ScrollToEnd(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();
    }
}
