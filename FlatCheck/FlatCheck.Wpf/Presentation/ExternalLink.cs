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
// the licence's and the components' links, the main window for the links its tabs show. The
// main window also opens the manual here, on its Help button and F1 (R-141).

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Navigation;
using NLog;

namespace FlatCheck.Wpf
{
    /// <summary>Opens an address in the system's browser: a clicked
    /// <see cref="System.Windows.Documents.Hyperlink"/>'s, or the manual's (R-141).</summary>
    internal static class ExternalLink
    {
        /// <summary>The diagnostic log: the link that could not be opened (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

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

            // a relative address has no absolute form (reading it throws): its text goes to the
            // shell as it is, which refuses it, and the refusal is logged
            Open(e.Uri.IsAbsoluteUri ? e.Uri.AbsoluteUri : e.Uri.OriginalString);
        }

        /// <summary>
        /// Opens an address in the system's browser: a clicked link's, or the manual's for the
        /// Help button and F1 (R-141). A system without an associated browser is only logged.
        /// </summary>
        /// <param name="address">The address: an absolute http or https one for every caller
        /// today; anything the shell cannot open is logged.</param>
        internal static void Open(string address)
        {
            try
            {
                Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                Log.Debug(ex, "the link " + address + " could not be opened in a browser");
            }
        }
    }
}
