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

// ExternalLink.cs - an address opened in the system's browser: a link clicked in the "Despre"
// window, and the manual on the Help button and F1 (R-186, ADR 0102).

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Navigation;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>Opens an address in the system's browser: a clicked
    /// <see cref="System.Windows.Documents.Hyperlink"/>'s, or the manual's (R-186).</summary>
    internal static class ExternalLink
    {
        /// <summary>The diagnostic log: an address that could not be opened (R-171, ADR 0069).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>
        /// Opens a clicked link in the system's browser and marks its navigation handled: outside
        /// a navigation window a Hyperlink only raises RequestNavigate, which the window catches
        /// on its root and passes here. A relative link never reaches the shell: reading its
        /// absolute form throws, and that is logged like any refusal.
        /// </summary>
        /// <param name="e">The navigation the link raised; marked handled.</param>
        internal static void Open(RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try { Start(e.Uri.AbsoluteUri); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // OriginalString, not AbsoluteUri: a relative link throws there, which is what this
                // catch took, and would throw again out of the handler and close the window (ADR
                // 0069, after DarksCheck's review).
                Log.Debug(ex, "the link " + e.Uri.OriginalString + " could not be opened in a browser");
            }
        }

        /// <summary>
        /// Opens an address in the system's browser: the manual's, for the Help button and F1
        /// (R-186). A refusal by the shell or a computer without a browser is no reason for the
        /// window to close: it is written to the diagnostic log at Debug and swallowed.
        /// </summary>
        /// <param name="address">The address: an absolute https one for every caller today;
        /// anything the shell cannot open is only logged.</param>
        internal static void Open(string address)
        {
            try { Start(address); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                Log.Debug(ex, "the link " + address + " could not be opened in a browser");
            }
        }

        /// <summary>
        /// Hands an address to the shell, which opens it in the system's browser. UseShellExecute
        /// is mandatory on .NET Core: without it Process.Start runs the address as a program.
        /// </summary>
        /// <param name="address">The absolute address.</param>
        static void Start(string address) => Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
}
