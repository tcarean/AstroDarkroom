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

// AboutWindow.xaml.cs - the "Despre" window: GPLv3 section 5(d) asks an interactive program to
// show its notice. Only the dark title bar and the opening of links remain here.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>The "Despre" window (R-119), over an <see cref="AboutViewModel"/>.</summary>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// Loads the XAML, binds a new <see cref="AboutViewModel"/>, puts the largest icon frame
        /// in the logo and asks for the dark title bar once there is a handle.
        /// </summary>
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = new AboutViewModel();
            Logo.Source = AppIcon.Largest();
            SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        }

        /// <summary>The diagnostic log: a link that could not be opened (R-171, ADR 0069).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>
        /// Opens a hyperlink in the system's browser, through the shell; a refusal by the shell
        /// or a missing handler is swallowed, and written to the diagnostic log at Debug.
        /// </summary>
        /// <param name="sender">The hyperlink; not read.</param>
        /// <param name="e">The navigation; its URI is opened and it is marked handled.</param>
        void OpenLink(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // OriginalString, not AbsoluteUri: a relative link throws there, which is what this
                // catch took, and would throw again out of the handler and close the window (ADR
                // 0069, after DarksCheck's review).
                Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened in a browser");
            }
        }

        /// <summary>The close button.</summary>
        /// <param name="sender">The button; not read.</param>
        /// <param name="e">The click; not read.</param>
        void Close(object sender, RoutedEventArgs e) => Close();
    }
}
