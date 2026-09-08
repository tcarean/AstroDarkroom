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

using System.Globalization;

namespace MindfulnessReminder.Core;

/// <summary>
/// The text under the mouse when the tray icon is hovered: the banner, then how long until
/// the next reminder.
///
/// It is a pure function rather than a line inside the tray host because of the length
/// limit. NotifyIcon.Text is capped at 63 characters and THROWS above it, so the one thing
/// really worth testing here is that the longest string this can produce still fits.
/// </summary>
public static class TrayTooltip
{
    /// <summary>The Shell_NotifyIcon tooltip limit that NotifyIcon.Text enforces.</summary>
    public const int MaxLength = 63;

    /// <summary>
    /// <paramref name="remaining"/> is null when nothing is being counted -- the feature is
    /// off, or the workstation is locked -- and the tooltip is then just the banner, the
    /// same as it read before this feature existed.
    /// </summary>
    public static string Format(string banner, TimeSpan? remaining)
    {
        ArgumentNullException.ThrowIfNull(banner);

        var text = remaining is { } left
            ? banner + " - next in " + MinutesLeft(left) + " min"
            : banner;

        // Defensive: every string this method can build fits, and a test says so. Truncating
        // rather than throwing means a longer banner one day degrades the tooltip instead of
        // taking the tray icon down with it.
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }

    /// <summary>
    /// Rounded UP, and never below 1. A countdown reading "0 min" through its last minute
    /// looks broken, and the ceiling is what makes it read "30 min" the instant it restarts
    /// rather than "29".
    /// </summary>
    private static string MinutesLeft(TimeSpan left)
        => Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)).ToString(CultureInfo.InvariantCulture);
}
