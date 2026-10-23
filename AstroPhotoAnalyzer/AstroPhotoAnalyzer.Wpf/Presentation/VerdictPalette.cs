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

// VerdictPalette.cs - what colour a verdict has.
//
// The colour is derived from the label the core writes (VerdictText.Label), not from a list of
// its own: a new verdict in the core would be coloured neutral, not green - the safe direction
// for a mistake.

using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The colour of a verdict, from its label: green for "OK", amber for "ATENTIE", red for
    /// "PROBLEMA", neutral grey for anything else.
    /// </summary>
    internal static class VerdictPalette
    {
        internal static readonly Color Good = Color.FromRgb(0x3F, 0xB9, 0x50);
        internal static readonly Color Warn = Color.FromRgb(0xE3, 0xB3, 0x41);
        internal static readonly Color Bad = Color.FromRgb(0xF8, 0x51, 0x49);
        internal static readonly Color Neutral = Color.FromRgb(0x8B, 0x94, 0x9E);

        /// <summary>The colour of a verdict label.</summary>
        /// <param name="label">
        /// The label as the core writes it (VerdictText.Label); null or any other text is neutral.
        /// </param>
        /// <returns>
        /// <see cref="Good"/>, <see cref="Warn"/>, <see cref="Bad"/> or <see cref="Neutral"/>.
        /// </returns>
        internal static Color For(string label)
        {
            if (label == VerdictText.Label(Verdict.Ok)) return Good;
            if (label == VerdictText.Label(Verdict.Attention)) return Warn;
            if (label == VerdictText.Label(Verdict.Problem)) return Bad;
            return Neutral;
        }

        /// <summary>The colour of a verdict label as a brush.</summary>
        /// <param name="label">The label, as for <see cref="For"/>.</param>
        /// <returns>A frozen solid brush of that colour, usable from any thread.</returns>
        internal static Brush BrushFor(string label)
        {
            var b = new SolidColorBrush(For(label));
            b.Freeze();
            return b;
        }
    }
}
