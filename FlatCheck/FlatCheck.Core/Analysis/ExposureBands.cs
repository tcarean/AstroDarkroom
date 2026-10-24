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

// ExposureBands.cs - the bands of the exposure verdict, as a value.
//
// WHY IT EXISTS. Classify used to choose inline between six constants of Th, by the --sky
// flag. When the window's histogram appeared, which draws the same bands over the curves,
// that choice would have had to be made a second time - and a second choice between the same
// constants would have been a second definition of the verdict, which can drift out of step
// silently. This way the green band on the drawing IS by construction the band that votes.
//
// On the sky there is no "acceptable, but rather low" band: the target starts right at the
// too-low threshold, so TooLow == Lo. Not an oversight, it is the very shape of the scale - a
// sky flat MUST stay modest, and "too low" and "below the target" are the same thing there.

namespace FlatCheck
{
    /// <summary>
    /// The thresholds the median of the central window is read against, in percent of
    /// (white - black). The same scale all the figures of the mode are on.
    /// </summary>
    internal readonly struct ExposureBands
    {
        /// <summary>Thresholds for a flat on the sky, not on a panel.</summary>
        internal readonly bool Sky;

        /// <summary>Below this, TOO LOW.</summary>
        internal readonly double TooLow;

        /// <summary>The target band, ends inclusive on the right (<c>cen &lt;= Hi</c>).</summary>
        internal readonly double Lo, Hi;

        /// <summary>Above this, TOO HIGH.</summary>
        internal readonly double TooHigh;

        /// <summary>The middle of the target, for the suggestion in stops.</summary>
        internal readonly double Target;

        /// <summary>
        /// Holds the five thresholds of one scale; only <see cref="For"/> builds one.
        /// </summary>
        /// <param name="sky">True for the sky scale.</param>
        /// <param name="tooLow">Below this, too low; percent of (white - black).</param>
        /// <param name="lo">The low end of the target band, percent.</param>
        /// <param name="hi">The high end of the target band, inclusive, percent.</param>
        /// <param name="tooHigh">Above this, too high, percent.</param>
        /// <param name="target">The middle of the target, percent.</param>
        ExposureBands(bool sky, double tooLow, double lo, double hi, double tooHigh, double target)
        {
            Sky = sky; TooLow = tooLow; Lo = lo; Hi = hi; TooHigh = tooHigh; Target = target;
        }

        /// <summary>
        /// The scale of a panel flat (<see cref="Th.ExpoTooLow"/> .. <see cref="Th.ExpoTarget"/>)
        /// or of a sky flat (<see cref="Th.ExpoSkyLo"/> .. <see cref="Th.ExpoSkyTarget"/>).
        /// </summary>
        /// <param name="sky">True for a sky flat (the --sky flag).</param>
        /// <returns>The bands the verdict is given on and the window draws.</returns>
        internal static ExposureBands For(bool sky) => sky
            ? new ExposureBands(true, Th.ExpoSkyLo, Th.ExpoSkyLo, Th.ExpoSkyHi, Th.ExpoSkyTooHigh, Th.ExpoSkyTarget)
            : new ExposureBands(false, Th.ExpoTooLow, Th.ExpoLo, Th.ExpoHi, Th.ExpoTooHigh, Th.ExpoTarget);

        /// <summary>
        /// true when the scale has an "acceptable, but rather low" band between too low and the
        /// target. On the sky it has none, and the drawing must not invent one of zero width.
        /// </summary>
        internal bool HasLowShoulder => Lo > TooLow;
    }
}
