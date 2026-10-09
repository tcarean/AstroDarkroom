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

using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace LightsCheck.Wpf;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
        Logo.Source = AppIcon.Largest();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    /// <summary>Opens the link in the system browser; UseShellExecute is what makes a URL open on .NET Core.</summary>
    private void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
