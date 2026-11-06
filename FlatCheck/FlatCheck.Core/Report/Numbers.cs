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

using System;
using System.Globalization;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// The number formats the reports share, the same in both languages: a difference with its
    /// sign written out, where what rounds to zero is shown as zero (R-129).
    /// </summary>
    internal static class Numbers
    {
        /// <summary>
        /// A difference with its sign written explicitly: "+0.34", "-0.34", and "+0.00" for
        /// whatever rounds to zero at the decimals shown, a negative included.
        ///
        /// Not the format with sections "+0.00;-0.00": for a negative that rounds to zero .NET
        /// formats with the POSITIVE section and still puts the minus in front, and out comes
        /// "-+0.00" (seen on 2026-11-02 on a master that was the model of a lensfun profile
        /// itself). The digits are formatted first and the sign decided on them, so the sign and
        /// the digits cannot disagree. <see cref="LightReport.Signed"/> is the one exception that
        /// keeps "-0.00", for the parity with the Python prototype.
        /// </summary>
        /// <param name="v">The value; NaN and the infinities come back as .NET writes them.</param>
        /// <param name="decimals">How many decimals to show, 0 or more.</param>
        /// <param name="provider">The culture of the decimal separator; null for the current
        /// one, which both applications keep invariant.</param>
        /// <returns>"+" or "-" and the absolute value with <paramref name="decimals"/> decimals.</returns>
        internal static string Signed(double v, int decimals, IFormatProvider provider = null)
        {
            if (!double.IsFinite(v)) return v.ToString(provider);
            string digits = Math.Abs(v).ToString(Pattern(decimals), provider);
            return (v > 0 || IsZero(digits) ? "+" : "-") + digits;
        }

        /// <summary>
        /// Whether <paramref name="v"/> reads as zero once written with
        /// <paramref name="decimals"/> decimals, so that a word about its sign can agree with its
        /// digits ("+0.00 %" with no "(darker)" after it).
        /// </summary>
        /// <param name="v">The value.</param>
        /// <param name="decimals">How many decimals it will be shown with, 0 or more.</param>
        /// <returns>True when every digit shown would be 0; false for NaN and the infinities.</returns>
        internal static bool RoundsToZero(double v, int decimals)
            => double.IsFinite(v) && IsZero(Math.Abs(v).ToString(Pattern(decimals), CultureInfo.InvariantCulture));

        /// <summary>The custom format with the decimals asked for: "0", "0.0", "0.00", ...</summary>
        /// <param name="decimals">How many decimals, 0 or more.</param>
        /// <returns>The format string.</returns>
        static string Pattern(int decimals) => decimals > 0 ? "0." + new string('0', decimals) : "0";

        /// <summary>Whether a formatted absolute value holds no digit other than 0.</summary>
        /// <param name="digits">The formatted value, sign-less.</param>
        /// <returns>True when it reads as zero.</returns>
        static bool IsZero(string digits) => !digits.Any(ch => ch >= '1' && ch <= '9');
    }
}
