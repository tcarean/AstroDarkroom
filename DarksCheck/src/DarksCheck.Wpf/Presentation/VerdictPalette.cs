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

// Copied from LightsCheck (which copied FlatCheck), the word list cut to this report's three
// verdict words; a fix in the mechanics goes there first (ADR 0009).

using System.Windows.Media;
using DarksCheck.Core;

namespace DarksCheck.Wpf;

/// <summary>
/// The colours of the verdict words the report uses (FlatCheck's palette): the word decides
/// the colour, so a new verdict in the core colours itself without a XAML change.
/// </summary>
internal static class VerdictPalette
{
    internal static readonly Color Good = Color.FromRgb(0x3F, 0xB9, 0x50);
    internal static readonly Color Fair = Color.FromRgb(0x9C, 0xCC, 0x65);
    internal static readonly Color Warn = Color.FromRgb(0xE3, 0xB3, 0x41);
    internal static readonly Color Bad = Color.FromRgb(0xF8, 0x51, 0x49);
    internal static readonly Color Neutral = Color.FromRgb(0x8B, 0x94, 0x9E);
    internal static readonly Color Accent = Color.FromRgb(0x4E, 0xA8, 0xDE);

    /// <summary>
    /// Severity of a verdict word: 0 good (OK), 2 attention (ATENTIE), 3 bad (EROARE), -1 neutral
    /// (NEMASURAT, empty), 1 anything else. The word is read by its start, case-insensitively.
    /// </summary>
    /// <param name="verdict">The verdict word or the text it starts; null counts as empty.</param>
    /// <returns>The severity, from -1 to 3.</returns>
    internal static int Severity(string? verdict)
    {
        string v = (verdict ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0 || v.StartsWith("NEMASURAT", StringComparison.Ordinal))
        {
            return -1;
        }

        if (v.StartsWith("OK", StringComparison.Ordinal))
        {
            return 0;
        }

        if (v.StartsWith("ATENTIE", StringComparison.Ordinal))
        {
            return 2;
        }

        if (v.StartsWith("EROARE", StringComparison.Ordinal))
        {
            return 3;
        }

        return 1;
    }

    /// <summary>The colour of a verdict word, by its <see cref="Severity"/>.</summary>
    /// <param name="verdict">The verdict word; null counts as empty (neutral).</param>
    /// <returns>One of the palette's colours.</returns>
    internal static Color For(string? verdict) => Severity(verdict) switch
    {
        0 => Good,
        1 => Fair,
        2 => Warn,
        3 => Bad,
        _ => Neutral,
    };

    /// <summary>
    /// The colour of a match verdict (R-062): direct is good, -opt and cosmetic are attention,
    /// unmatched is bad, unmeasured neutral.
    /// </summary>
    /// <param name="v">The match verdict of the core.</param>
    /// <returns>One of the palette's colours.</returns>
    internal static Color For(MatchVerdict v) => v switch
    {
        MatchVerdict.Direct => Good,
        MatchVerdict.WithOpt => Warn,
        MatchVerdict.CosmeticOnly => Warn,
        MatchVerdict.Unmatched => Bad,
        _ => Neutral,
    };

    /// <summary>The colour of a verdict word as a frozen brush.</summary>
    /// <param name="verdict">The verdict word; null counts as empty (neutral).</param>
    /// <returns>A frozen solid brush, usable from any thread.</returns>
    internal static Brush BrushFor(string? verdict)
    {
        SolidColorBrush b = new(For(verdict));
        b.Freeze();
        return b;
    }
}
