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

// IccPickerWindow.xaml.cs - legatura cu XAML-ul ferestrei "Din profil ICC..." (R-132).
// Comportamentul e in IccPickerViewModel; aici sunt dialogul de fisier (ViewModel-ul nu
// deschide dialoguri), inchiderea cu rezultat si bara de titlu intunecata.

using System.Windows;
using System.Windows.Input;

namespace AstroPhotoAnalyzer.Wpf
{
    public partial class IccPickerWindow : Window
    {
        public IccPickerWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        IccPickerViewModel Vm => (IccPickerViewModel)DataContext;

        void ChooseFile(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Alege profilul ICC al hartiei pe imprimanta ei",
                Filter = "Profile ICC (*.icc;*.icm)|*.icc;*.icm|Toate fisierele|*.*",
                Multiselect = false,
            };
            if (dlg.ShowDialog(this) == true) Vm.AddFile(dlg.FileName);
        }

        void Use(object sender, RoutedEventArgs e)
        {
            if (Vm.CanUse) DialogResult = true;
        }

        void UseOnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Dublu-clic pe un rand, nu pe bara de derulare sau pe golul listei.
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject d && ItemsControl_ContainerOf(d) && Vm.CanUse) DialogResult = true;
        }

        bool ItemsControl_ContainerOf(DependencyObject d)
            => System.Windows.Controls.ItemsControl.ContainerFromElement(List, d) != null;
    }
}
