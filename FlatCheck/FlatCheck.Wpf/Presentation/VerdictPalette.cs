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

// VerdictPalette.cs - which colour a verdict has.
//
// The colour is derived from Stats.Severity, NOT from a list of strings of its own. The core
// already knows how to order the verdicts of both vocabularies (OK/SUSPECT/PROBLEMA and
// PERFECT/BUN/ACCEPTABIL/REFA), including the rule "anything unknown is treated as the worst".
// A second list here would have meant that a new verdict added in the core shows in the window
// coloured wrong - that is, green - which is the worst possible direction for a mistake of
// this kind.
//
// The two exceptions are NEDETERMINAT, which in Stats sits at severity 2 because there only the
// exit code matters ("I do not know" asks for the same reaction as "suspect"), and INAPLICABIL
// (a field too wide for --light, or a file the Expo tab refuses as not a raw flat), which
// Stats leaves at 3 like any word it does not know.
// Visually, though, neither is a warning, each is an absence: they are coloured neutral, so
// they do not look like a bad result when in fact there is no result at all.

using System.Windows.Media;

namespace FlatCheck.Wpf
{
    /// <summary>The colour of a verdict, derived from <see cref="Stats.Severity"/>, neutral
    /// for the absence of one.</summary>
    internal static class VerdictPalette
    {
        internal static readonly Color Perfect = Color.FromRgb(0x3F, 0xB9, 0x50);
        internal static readonly Color Good = Color.FromRgb(0x9C, 0xCC, 0x65);
        internal static readonly Color Warn = Color.FromRgb(0xE3, 0xB3, 0x41);
        internal static readonly Color Bad = Color.FromRgb(0xF8, 0x51, 0x49);
        internal static readonly Color Neutral = Color.FromRgb(0x8B, 0x94, 0x9E);

        /// <summary>The colour of a verdict: by its severity, neutral when it is
        /// inconclusive.</summary>
        /// <param name="verdict">A verdict word of either ladder; unknown words get the worst
        /// colour.</param>
        /// <returns>One of the five colours above.</returns>
        internal static Color For(string verdict)
        {
            if (IsInconclusive(verdict)) return Neutral;

            return Stats.Severity(verdict) switch
            {
                0 => Perfect,
                1 => Good,
                2 => Warn,
                _ => Bad,
            };
        }

        /// <summary>A frozen brush in a verdict's colour.</summary>
        /// <param name="verdict">A verdict word.</param>
        /// <returns>A new frozen <see cref="SolidColorBrush"/>.</returns>
        internal static Brush BrushFor(string verdict)
        {
            var b = new SolidColorBrush(For(verdict));
            b.Freeze();   // bound from several threads; frozen it is also cheaper to draw
            return b;
        }

        /// <summary>
        /// "It ran but does not conclude" (an inconclusive series) and "does not apply" (a
        /// field too wide) are not bad qualities, they are the lack of an answer.
        /// </summary>
        /// <param name="verdict">A verdict word.</param>
        /// <returns>True for <see cref="LightSeriesAnalyzer.Undetermined"/> and
        /// <see cref="Stats.NotApplicable"/>.</returns>
        internal static bool IsInconclusive(string verdict)
            => verdict == LightSeriesAnalyzer.Undetermined
            || verdict == Stats.NotApplicable;
    }
}
