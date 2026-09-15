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

// MainWindow.xaml.cs - fereastra: alegerea folderului si bara de titlu intunecata.
//
// Tot ce inseamna decizie sta in MainViewModel. Aici raman doar cele doua lucruri care
// chiar au nevoie de o fereastra: dialogul de folder (un ViewModel care deschide dialoge
// nu se mai poate testa) si cererea catre DWM de a intuneca bara de titlu.

using System.Windows;

namespace FlatCheck.Wpf
{
    public partial class MainWindow : Window
    {
        readonly MainViewModel _vm = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // Bara de titlu se poate colora abia dupa ce fereastra are un handle.
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);

            // Fara asta, inchiderea ferestrei in mijlocul unei serii de 32 de cadre ar
            // lasa firul de calcul sa duca treaba pana la capat degeaba.
            Closed += (_, _) => _vm.CancelPending();
        }

        /// <summary>
        /// Pornirea: daca s-a dat un folder in linia de comanda se foloseste, altfel se
        /// intreaba imediat. Aplicatia n-are ce arata fara un folder, deci a astepta un
        /// clic in plus ar fi doar un pas gol.
        /// </summary>
        internal void Start(string folder)
        {
            if (!string.IsNullOrEmpty(folder)) { _ = _vm.OpenAsync(folder); return; }

            Dispatcher.BeginInvoke(new System.Action(() => Choose()));
        }

        void ChooseFolder(object sender, RoutedEventArgs e) => Choose();

        /// <summary>Butonul "Despre" si F1, prin ApplicationCommands.Help - vezi MainWindow.xaml.</summary>
        void ShowAbout(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
            => new AboutWindow { Owner = this }.ShowDialog();

        void Choose()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Alege folderul de lucru al sesiunii Siril",
                Multiselect = false,
            };

            if (dlg.ShowDialog(this) == true) _ = _vm.OpenAsync(dlg.FolderName);
        }
    }
}
