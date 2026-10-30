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

// AboutWindow.xaml.cs - the "Despre" window.
//
// It exists because GPLv3 section 5(d) asks an interactive program to show its copyright
// notice, the absence of warranty and the pointer to the licence - the console gives them
// at --help, the window had nowhere to - and because LibRaw's LGPL asks for the library's
// notice beside ours. All the text comes through AboutViewModel; here remain only the two
// window things: the dark title bar and opening the links in the browser.

using System.Windows;
using System.Windows.Navigation;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// The modal "Despre" window: the icon, the version, the copyright, the GPLv3 notice and the
    /// third-party components, all bound to <see cref="AboutViewModel"/>.
    /// </summary>
    public partial class AboutWindow : Window
    {
        /// <summary>Builds the window over a new <see cref="AboutViewModel"/>, with the
        /// largest icon and the dark title bar.</summary>
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = new AboutViewModel();
            Logo.Source = AppIcon.Largest();

            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        }

        /// <summary>Opens a link of the window, the licence's or a component's, in the system's
        /// browser, through <see cref="ExternalLink.Open"/>.</summary>
        /// <param name="sender">The window, where the link's event arrives.</param>
        /// <param name="e">The navigation; marked handled.</param>
        void OpenLink(object sender, RequestNavigateEventArgs e) => ExternalLink.Open(e);

        /// <summary>The "Inchide" button's handler: closes the window.</summary>
        /// <param name="sender">The button.</param>
        /// <param name="e">The click.</param>
        void Close(object sender, RoutedEventArgs e) => Close();
    }
}
