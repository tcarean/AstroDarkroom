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

// ChannelPalette.cs - the colour of a CFA channel on the histogram.
//
// WHY IT IS NOT IN VerdictPalette. These are NOT verdicts and must never go through
// Stats.Severity: a red channel is not "worse" than a green one. Two colour systems on the same
// screen are risky precisely because they look alike, so it is worth saying: the verdict shows
// on a single pill, at the top; the other colours on the tab identify a channel.
//
// WHY IT IS NOT IN Dark.xaml. The choice is made from a char that comes from the result, that
// is from C#, not from a binding - and a resource dictionary could not decide on it.
//
// When the file did not say the order, all four channels get the accent colour. It is
// deliberate: four curves coloured R/G/G/B on an unknown mosaic would look exactly like a
// measurement, and would not be one.

using System.Windows.Media;

namespace FlatCheck.Wpf
{
    /// <summary>The colours of the CFA channels on the Exposure tab's histograms; never a
    /// verdict.</summary>
    internal static class ChannelPalette
    {
        internal static readonly Color R = Color.FromRgb(0xE0, 0x6C, 0x75);
        internal static readonly Color G = Color.FromRgb(0x7D, 0xC3, 0x83);
        internal static readonly Color B = Color.FromRgb(0x61, 0xAF, 0xEF);

        /// <summary>The same as AccentBr in the theme: "I do not know which colour it is"
        /// looks neutral.</summary>
        internal static readonly Color Unknown = Color.FromRgb(0x4E, 0xA8, 0xDE);

        /// <summary>The colour of a channel.</summary>
        /// <param name="colour">'R', 'G' or 'B' from the CFA order; anything else is
        /// unknown.</param>
        /// <returns>The channel's colour, or <see cref="Unknown"/>.</returns>
        internal static Color For(char colour) => colour switch
        {
            'R' => R,
            'G' => G,
            'B' => B,
            _ => Unknown,
        };

        /// <summary>A frozen brush in a channel's colour.</summary>
        /// <param name="colour">'R', 'G' or 'B'; anything else is unknown.</param>
        /// <returns>A new frozen <see cref="SolidColorBrush"/>.</returns>
        internal static Brush BrushFor(char colour)
        {
            var b = new SolidColorBrush(For(colour));
            b.Freeze();   // bound from several threads, and frozen it is also cheaper to draw
            return b;
        }
    }
}
