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

using System.Windows.Media;

namespace LightsCheck.Wpf;

/// <summary>
/// The colours of the verdict words the reports use (FlatCheck's palette): the word decides
/// the colour, so a new verdict in the core colours itself without a XAML change.
/// </summary>
internal static class VerdictPalette
{
    internal static readonly Color Good = Color.FromRgb(0x3F, 0xB9, 0x50);
    internal static readonly Color Fair = Color.FromRgb(0x9C, 0xCC, 0x65);
    internal static readonly Color Warn = Color.FromRgb(0xE3, 0xB3, 0x41);
    internal static readonly Color Bad = Color.FromRgb(0xF8, 0x51, 0x49);
    internal static readonly Color Neutral = Color.FromRgb(0x8B, 0x94, 0x9E);

    /// <summary>
    /// Severity of a verdict word, in either language (ADR 0022): 0 good, 1 fair, 2 attention,
    /// 3 bad, -1 neutral. The English words are chosen so that none starts like a word of
    /// another severity.
    /// </summary>
    /// <param name="verdict">
    /// The word, or the start of a longer verdict; null, empty and any case allowed.
    /// </param>
    /// <returns>The severity; 1 for a word not in the lists.</returns>
    internal static int Severity(string? verdict)
    {
        string v = (verdict ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0 || StartsWithAny(v, "NEDETERMINAT", "NEMASURAT", "NECITIT", "UNDETERMINED", "NOT MEASURED", "UNREAD"))
        {
            return -1;
        }

        if (v is "BUN" or "BUNA" or "OK" or "NU" or "DA" or "GOOD" or "NO" or "YES")
        {
            return v is "DA" or "YES" ? 2 : 0;
        }

        // The dithering verdict opens with its worst part; FARA / NO (no walking noise) is as
        // good as OK.
        if (StartsWithAny(v, "FARA", "OK", "NO "))
        {
            return 0;
        }

        if (StartsWithAny(v, "LA LIMITA", "POSIBIL", "ATENTIE", "MARGINAL", "POSSIBLE", "WARNING"))
        {
            return 2;
        }

        if (StartsWithAny(v, "SLAB", "INSUFICIENT", "EROARE", "BAD", "INSUFFICIENT", "ERROR"))
        {
            return 3;
        }

        return 1;
    }

    /// <summary>Whether a word starts with one of the given starts, ordinally.</summary>
    /// <param name="v">The word, in capitals.</param>
    /// <param name="starts">The starts to try.</param>
    /// <returns>True when one of them matches.</returns>
    private static bool StartsWithAny(string v, params string[] starts)
        => starts.Any(s => v.StartsWith(s, StringComparison.Ordinal));

    /// <summary>The colour of a verdict word, by its <see cref="Severity"/>.</summary>
    /// <param name="verdict">The word; null allowed.</param>
    /// <returns>One of the five colours.</returns>
    internal static Color For(string? verdict) => Severity(verdict) switch
    {
        0 => Good,
        1 => Fair,
        2 => Warn,
        3 => Bad,
        _ => Neutral,
    };

    /// <summary>The colour of a verdict word as a frozen brush.</summary>
    /// <param name="verdict">The word; null allowed.</param>
    /// <returns>A frozen solid brush.</returns>
    internal static Brush BrushFor(string? verdict)
    {
        SolidColorBrush b = new(For(verdict));
        b.Freeze();
        return b;
    }
}
