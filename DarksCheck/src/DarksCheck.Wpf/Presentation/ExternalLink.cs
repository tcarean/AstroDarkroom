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

// ExternalLink.cs - an address opened in the system's browser: the manual's, for the main
// window's Help button and F1 (R-125, ADR 0061). The About window, LightsCheck's copy, keeps its
// own link handler, which fails the same way.

using System.ComponentModel;
using System.Diagnostics;
using NLog;

namespace DarksCheck.Wpf;

/// <summary>Opens an address in the system's browser (R-125).</summary>
internal static class ExternalLink
{
    /// <summary>The diagnostic log: the address that could not be opened (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// Opens an address in the system's browser. UseShellExecute is what makes a URL open on .NET
    /// Core; without it Process.Start tries to run the address as a program. A computer without
    /// an associated browser, or an address the shell refuses, is a Debug line of the log and
    /// nothing more: the window stays (ADR 0025).
    /// </summary>
    /// <param name="address">The address, an absolute https one for every caller today; not
    /// null.</param>
    internal static void Open(string address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Debug(ex, "the link " + address + " could not be opened in a browser");
        }
    }
}
