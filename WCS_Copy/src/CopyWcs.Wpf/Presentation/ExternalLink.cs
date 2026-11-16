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

// ExternalLink.cs - the manual, opened in the system's browser for the Help button and F1
// (R-039, ADR 0016). After ImageVisualCompare's, whose guard it keeps: only a web address goes to
// the shell. The About window keeps its own link handler, a piece copied from DarksCheck.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NLog;

namespace CopyWcs.Wpf;

/// <summary>Opens a web address in the system's browser: the manual at a section (R-039).</summary>
internal static class ExternalLink
{
    /// <summary>The diagnostic log: an address that was not or could not be opened (R-043).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    /// <summary>
    /// Opens a web address in the system's browser. <c>UseShellExecute</c> is what opens a URL on
    /// .NET; a system with no browser for it throws, which only writes a Debug line in the log.
    /// The process the shell may return is not watched, so it is disposed at once.
    /// </summary>
    /// <param name="address">The address as written: an absolute http or https address for every
    /// caller; anything else is only logged, never handed to the shell.</param>
    internal static void Open(string address)
    {
        if (!IsWebAddress(address, out Uri? uri))
        {
            Log.Debug("the link " + address + " is not a web address, so it was not opened");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log.Debug(ex, "the link " + address + " could not be opened");
        }
    }

    /// <summary>
    /// Whether an address may go to the shell: an absolute http or https one. The shell resolves
    /// any other text against the working folder and the PATH, so a relative link would open a
    /// file or start a program, and a <c>file:</c> one would open a file.
    /// </summary>
    /// <param name="address">The address as written; empty is allowed.</param>
    /// <param name="uri">The parsed address when it is a web address; otherwise not to be read.</param>
    /// <returns>True for an absolute http or https address.</returns>
    internal static bool IsWebAddress(string address, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(address, UriKind.Absolute, out uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
