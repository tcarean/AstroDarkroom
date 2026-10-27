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

// Fmt.cs - the formatting of the report's numbers, with the invariant culture EXPLICIT.
//
// The console has InvariantGlobalization; the window cannot (WPF crashes without culture data)
// and sets the culture at start-up. If any number went through the current culture, "0.24" could
// become "0,24" on a Romanian Windows and Raport.txt would no longer be identical to the
// console's output. Every number with decimals in the report goes through here; some integers
// (counts, thresholds in levels or pixels, the seed) are interpolated as they are, which prints
// the same digits in both applications, since both run in the invariant culture.

using System.Globalization;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Formats the numbers of the report and of the window's texts in the invariant culture; NaN
    /// prints as "n/a" (except in <see cref="Int"/>, which takes no NaN).
    /// </summary>
    internal static class Fmt
    {
        static readonly CultureInfo I = CultureInfo.InvariantCulture;

        /// <summary>A number rounded to an integer: "12".</summary>
        /// <param name="v">The number; NaN allowed.</param>
        /// <returns>The number without decimals, or "n/a" for NaN.</returns>
        internal static string F0(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0", I);

        /// <summary>A number with one decimal: "12.3".</summary>
        /// <param name="v">The number; NaN allowed.</param>
        /// <returns>The number with one decimal, or "n/a" for NaN.</returns>
        internal static string F1(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0", I);

        /// <summary>A number with two decimals: "1.23".</summary>
        /// <param name="v">The number; NaN allowed.</param>
        /// <returns>The number with two decimals, or "n/a" for NaN.</returns>
        internal static string F2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", I);

        /// <summary>A number with three decimals: "0.123".</summary>
        /// <param name="v">The number; NaN allowed.</param>
        /// <returns>The number with three decimals, or "n/a" for NaN.</returns>
        internal static string F3(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.000", I);

        /// <summary>A number with four decimals: "0.0123".</summary>
        /// <param name="v">The number; NaN allowed.</param>
        /// <returns>The number with four decimals, or "n/a" for NaN.</returns>
        internal static string F4(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0000", I);

        /// <summary>With a sign: "+1.6", "-9.9", "0.0".</summary>
        /// <param name="v">The number, for example a difference in levels; NaN allowed.</param>
        /// <returns>
        /// The number with one decimal and its sign ("0.0" for what rounds to zero), or "n/a"
        /// for NaN.
        /// </returns>
        internal static string S1(double v) => double.IsNaN(v) ? "n/a" : v.ToString("+0.0;-0.0;0.0", I);

        /// <summary>
        /// A background level (0..1): two decimals on the grid ("0.17"), three when it has them
        /// ("0.162", the exact target; "0.172", the measured level). With F2 the exact target and
        /// the grid's row would have looked like the same number.
        /// </summary>
        /// <param name="v">The level, 0..1; NaN allowed.</param>
        /// <returns>The level with two or three decimals, or "n/a" for NaN.</returns>
        internal static string Level(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00#", I);

        /// <summary>
        /// A Dmax: two decimals when it has them, at least one - "2.0", "1.5", "2.35". With F1,
        /// --dmax 2.35 showed as "Dmax 2.4" and the report seemed to have measured another paper.
        /// </summary>
        /// <param name="v">The Dmax, an optical density; NaN allowed.</param>
        /// <returns>The Dmax with one or two decimals, or "n/a" for NaN.</returns>
        internal static string Dmax(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0#", I);

        /// <summary>A percentage with one decimal: "12.3 %".</summary>
        /// <param name="v">The percentage, 0..100 (not a fraction); NaN allowed.</param>
        /// <returns>The percentage followed by " %", or "n/a %" for NaN.</returns>
        internal static string Pct(double v) => F1(v) + " %";

        /// <summary>
        /// The core's one piece of markup: a part of a "because" that must catch the eye, between
        /// <c>**</c> marks. The window's Raport tab shows it in bold; the text report strips the
        /// marks with <see cref="Plain"/>.
        /// </summary>
        /// <param name="s">The part to emphasise, without marks of its own.</param>
        /// <returns>The part between <c>**</c> marks.</returns>
        internal static string Strong(string s) => "**" + s + "**";

        /// <summary>The text without the <c>**</c> marks of <see cref="Strong"/>.</summary>
        /// <param name="s">A text, possibly marked; null gives null.</param>
        /// <returns>The same text with every <c>**</c> removed.</returns>
        internal static string Plain(string s) => s?.Replace("**", "");

        /// <summary>A size in megabytes with one decimal: "45.6 MB".</summary>
        /// <param name="v">The size, in MB; NaN allowed.</param>
        /// <returns>The size followed by " MB", or "n/a MB" for NaN.</returns>
        internal static string Mb(double v) => F1(v) + " MB";

        /// <summary>An integer without group separators: "4078".</summary>
        /// <param name="v">The integer.</param>
        /// <returns>Its digits, with a leading '-' when negative.</returns>
        internal static string Int(long v) => v.ToString(I);

        /// <summary>"5938 x 3888".</summary>
        /// <param name="w">The width, in pixels.</param>
        /// <param name="h">The height, in pixels.</param>
        /// <returns>The width and the height joined by " x ".</returns>
        internal static string Dim(int w, int h) => Int(w) + " x " + Int(h);
    }
}
