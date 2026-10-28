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

// ExternalLink.cs - a clicked link, opened in the system's browser.
//
// Outside a navigation window a Hyperlink only raises RequestNavigate, a routed event: each
// window that shows links catches it on its root and calls Open here - the "Despre" window for
// the licence's and the components' links, the main window for the links its tabs show.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Navigation;

namespace FlatCheck.Wpf
{
    /// <summary>Opens the address of a clicked <see cref="System.Windows.Documents.Hyperlink"/>
    /// in the system's browser.</summary>
    internal static class ExternalLink
    {
        /// <summary>
        /// Opens the link in the system's browser. UseShellExecute is mandatory on .NET Core -
        /// without it Process.Start tries to execute the URL as a program. A system without an
        /// associated browser throws; that is no reason for the window to crash, so it is only
        /// logged.
        /// </summary>
        /// <param name="e">The navigation the link raised; marked handled.</param>
        internal static void Open(RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                NLog.LogManager.GetLogger("Window").Debug(ex, "the link " + e.Uri + " could not be opened in a browser");
            }
        }
    }
}
