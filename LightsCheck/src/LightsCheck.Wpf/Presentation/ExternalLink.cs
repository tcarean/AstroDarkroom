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

using System.ComponentModel;
using System.Diagnostics;
using NLog;

namespace LightsCheck.Wpf;

/// <summary>
/// Opens an address in the system's browser: a link clicked in the About window, or the manual's
/// for the main window's Help button and F1 (R-091, ADR 0060).
/// </summary>
internal static class ExternalLink
{
    /// <summary>The diagnostic log: a link that could not be opened (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// Opens the link in the system's browser; UseShellExecute is what makes a URL open on .NET
    /// Core. A link that cannot be opened (no browser starts, or the link is relative and has no
    /// absolute address) is a Debug line in the diagnostic log with the reason, and the window
    /// stays.
    /// </summary>
    /// <param name="link">The address: an absolute http or https one for every caller today; a
    /// relative one is refused before anything is started, and logged.</param>
    internal static void Open(Uri link)
    {
        try
        {
            Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // OriginalString, not AbsoluteUri: a relative link throws there, which is what this
            // catch took, and would throw again out of the handler; the log never stops the
            // window (ADR 0029).
            Log.Debug(ex, "the link " + link.OriginalString + " could not be opened in a browser");
        }
    }
}
