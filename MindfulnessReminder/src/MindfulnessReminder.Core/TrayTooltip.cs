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
/// The text under the mouse when the tray icon is hovered: the banner, then either how long
/// until the next reminder or why one is being held back.
///
/// It is a pure function rather than a line inside the tray host because of the length
/// limit. NotifyIcon.Text is capped at 63 characters and THROWS above it, so the one thing
/// really worth testing here is that the longest string this can produce still fits.
/// </summary>
public static class TrayTooltip
{
    /// <summary>
    /// The length the tooltip keeps to. NotifyIcon.Text throws above 127 characters on .NET 6
    /// and later, and above 63 on .NET Framework, whose limit this is.
    /// </summary>
    public const int MaxLength = 63;

    /// <summary>
    /// <paramref name="remaining"/> is null when nothing is being counted -- the feature is
    /// off, or the workstation is locked -- and the tooltip is then just the banner, the
    /// same as it read before this feature existed.
    ///
    /// A <paramref name="suppression"/> other than None replaces the countdown rather than
    /// joining it. What is being counted at that point is the retry gap, seconds rather than
    /// minutes, and a countdown stuck at "1 min" through an hour-long call is the one thing
    /// that would make a working feature look broken. The reason is also the only way to
    /// tell a held-back reminder from a stopped one without opening anything.
    /// </summary>
    public static string Format(
        string banner,
        TimeSpan? remaining,
        ReminderSuppression suppression = ReminderSuppression.None)
    {
        ArgumentNullException.ThrowIfNull(banner);

        var text = suppression switch
        {
            ReminderSuppression.MicrophoneInUse => banner + " - paused while your mic is on",
            ReminderSuppression.FullScreen => banner + " - paused for full screen",
            _ => remaining is { } left
                ? banner + " - next in " + MinutesLeft(left) + " min"
                : banner,
        };

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
