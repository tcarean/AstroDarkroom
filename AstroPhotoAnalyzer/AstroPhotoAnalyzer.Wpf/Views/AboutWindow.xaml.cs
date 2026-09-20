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

// AboutWindow.xaml.cs - fereastra "Despre": GPLv3 sectiunea 5(d) cere ca un program
// interactiv sa-si arate notita. Aici raman doar bara de titlu intunecata si deschiderea
// link-urilor.

using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace AstroPhotoAnalyzer.Wpf
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = new AboutViewModel();
            Logo.Source = AppIcon.Largest();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        }

        void OpenLink(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { }
            catch (System.InvalidOperationException) { }
        }

        void Close(object sender, RoutedEventArgs e) => Close();
    }
}
