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

using System.Reflection;

namespace MindfulnessReminder.Core;

/// <summary>
/// The licence notice the app shows about itself. GPLv3 asks that every copy carry the
/// copyright notice and the disclaimer of warranty; a tray app has no console to print
/// them on, so they live behind the tray menu's About item -- the one place a user goes
/// looking for them, and the only interface here that would ever display legal notices.
///
/// The copyright string has a single source of truth, <c>&lt;Copyright&gt;</c> in
/// Directory.Build.props, which MSBuild turns into AssemblyCopyrightAttribute. Don't
/// hardcode it anywhere: the literal below is a fallback for the case where the attribute
/// goes missing or blank, not a second place to edit.
/// </summary>
public static class LicenseInfo
{
    /// <summary>
    /// Used only if the assembly attribute is missing or blank; Directory.Build.props is the
    /// truth.
    /// </summary>
    private const string Fallback = "Copyright (C) 2026 Tudor Carean";

    /// <summary>
    /// The disclaimer of warranty, plus where to find the licence itself. LICENSE is copied
    /// next to the executable by the app project, so pointing at it is not a promise the
    /// deployment can break.
    /// </summary>
    public const string Warranty =
        "This program comes with ABSOLUTELY NO WARRANTY, not even the implied warranty of "
        + "merchantability or fitness for a particular purpose.\n\n"
        + "It is free software: you may redistribute it and modify it under the terms of the "
        + "GNU General Public License v3 -- or, at your option, any later version. The full "
        + "text is in the LICENSE file next to the executable, and at "
        + "https://www.gnu.org/licenses/gpl-3.0.html";

    /// <summary>The copyright notice, as read from the assembly the type was built into.</summary>
    public static string Copyright { get; } = ReadCopyright();

    /// <summary>
    /// Everything the About dialog shows, in order: the banner on the first line, the
    /// copyright on the second, the disclaimer below. It is the same arrangement the
    /// command-line tools print at the top of every run - one look says which version,
    /// whose it is, and under what terms.
    /// </summary>
    public static string AboutText =>
        VersionInfo.Banner + "\n" + Copyright + "\n\n" + Warranty;

    /// <summary>
    /// Reads the copyright notice from the assembly's <see cref="AssemblyCopyrightAttribute"/>.
    /// </summary>
    /// <returns>
    /// The notice, or <see cref="Fallback"/> when the attribute is missing or blank.
    /// </returns>
    private static string ReadCopyright()
    {
        var copyright = typeof(LicenseInfo).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        return string.IsNullOrWhiteSpace(copyright) ? Fallback : copyright;
    }
}
