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

    /// <summary>Severity of a verdict word: 0 good, 1 fair, 2 attention, 3 bad, -1 neutral.</summary>
    internal static int Severity(string? verdict)
    {
        string v = (verdict ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0 || v.StartsWith("NEDETERMINAT", StringComparison.Ordinal) || v.StartsWith("NEMASURAT", StringComparison.Ordinal) || v.StartsWith("NECITIT", StringComparison.Ordinal))
        {
            return -1;
        }

        if (v is "BUN" or "BUNA" or "OK" or "NU" or "DA")
        {
            return v == "DA" ? 2 : 0;
        }

        if (v.StartsWith("LA LIMITA", StringComparison.Ordinal) || v.StartsWith("POSIBIL", StringComparison.Ordinal) || v.StartsWith("ATENTIE", StringComparison.Ordinal))
        {
            return 2;
        }

        if (v.StartsWith("SLAB", StringComparison.Ordinal) || v.StartsWith("INSUFICIENT", StringComparison.Ordinal) || v.StartsWith("EROARE", StringComparison.Ordinal))
        {
            return 3;
        }

        return 1;
    }

    internal static Color For(string? verdict) => Severity(verdict) switch
    {
        0 => Good,
        1 => Fair,
        2 => Warn,
        3 => Bad,
        _ => Neutral,
    };

    internal static Brush BrushFor(string? verdict)
    {
        SolidColorBrush b = new(For(verdict));
        b.Freeze();
        return b;
    }
}
