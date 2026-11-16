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
using System.Windows.Navigation;
using ImageVisualCompare.Wpf.Logging;
using NLog;

namespace ImageVisualCompare.Wpf.Services;

/// <summary>
/// Opens an address in the system's browser: a link of the About window (R-072), or the manual
/// at a section for Help and F1 (R-066).
/// </summary>
internal static class ExternalLink
{
    /// <summary>The diagnostic log: an address that could not be opened (R-121).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// Opens a clicked link. Outside a navigation window a <c>Hyperlink</c> only raises this
    /// routed event, caught on the window's root.
    /// </summary>
    /// <param name="e">The navigation the link raised; marked handled.</param>
    internal static void Open(RequestNavigateEventArgs e)
    {
        e.Handled = true;

        // The address as written for a relative one: AbsoluteUri throws for it, and the shell then
        // refuses the text, which is logged.
        Open(e.Uri.IsAbsoluteUri ? e.Uri.AbsoluteUri : e.Uri.OriginalString);
    }

    /// <summary>
    /// Opens an address in the system's browser. <c>UseShellExecute</c> is what opens a URL on
    /// .NET; a system with no browser for it throws, which only writes a line in the log. The
    /// process the shell may return is not watched, so it is disposed at once.
    /// </summary>
    /// <param name="address">An absolute http or https address for every caller; anything the
    /// shell cannot open is logged.</param>
    internal static void Open(string address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Debug(ex, "the link " + address + " could not be opened");
        }
    }
}
