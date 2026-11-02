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

namespace ImageVisualCompare.Core;

/// <summary>
/// The widths of the two file names in the toolbar's pair label, <c>A.png  Change A…  vs  B.tif
/// Change B…</c> (R-065), in device-independent pixels. Each name takes its own width, however
/// long (ADR 0048); only when the five items do not fit the width the label has do the names give
/// up the excess, the longer one first, down to <see cref="MinName"/> each, and the buttons and
/// <c>vs</c> never shrink, so the button that changes B is never pushed out of sight by a long
/// name. A name shorter than <see cref="MinName"/> keeps its own width.
/// </summary>
/// <param name="NameAWidth">The width image A's name is given.</param>
/// <param name="NameBWidth">The width image B's name is given.</param>
public readonly record struct PairLabelLayout(double NameAWidth, double NameBWidth)
{
    /// <summary>The least a name gives up to: below it a file name says nothing.</summary>
    public const double MinName = 48;

    /// <summary>Lays the two names out in the width the label has.</summary>
    /// <param name="available">
    /// The width the whole label may take; positive infinity when nothing constrains it.
    /// </param>
    /// <param name="nameA">The width image A's name would take on its own, 0 or more.</param>
    /// <param name="nameB">The width image B's name would take on its own, 0 or more.</param>
    /// <param name="fixedWidth">
    /// The width of everything else in the label, the two buttons and <c>vs</c> with their
    /// margins, 0 or more.
    /// </param>
    /// <returns>
    /// The two widths: the names' own when they fit, the longer one
    /// cut to what is left otherwise, and both cut equally when that is not enough, never below
    /// <see cref="MinName"/> or the name's own width. The sum can exceed the room only at that
    /// floor, where the label is clipped.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">A width is negative or NaN.</exception>
    public static PairLabelLayout For(double available, double nameA, double nameB, double fixedWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nameA);
        ArgumentOutOfRangeException.ThrowIfNegative(nameB);
        ArgumentOutOfRangeException.ThrowIfNegative(fixedWidth);
        // A NaN name or fixed width is already refused above, since NaN carries the sign bit.
        if (double.IsNaN(available))
        {
            throw new ArgumentOutOfRangeException(nameof(available), "The width available cannot be NaN.");
        }

        double room = available - fixedWidth;
        if (nameA + nameB <= room)
        {
            return new PairLabelLayout(nameA, nameB);
        }

        // The longer name gives first: cut to what the shorter leaves, when that still keeps it
        // at least as long as the shorter one, and never below its floor (the second review of
        // 2026-10-30: a short name beside a long one in little room cut the long one under 48).
        double shorter = Math.Min(nameA, nameB);
        if (room - shorter >= shorter)
        {
            double longer = Math.Max(room - shorter, Math.Min(MinName, Math.Max(nameA, nameB)));
            return nameA >= nameB ? new PairLabelLayout(longer, nameB) : new PairLabelLayout(nameA, longer);
        }

        // Both give equally, each down to its floor.
        double half = Math.Max(room, 0) / 2;
        return new PairLabelLayout(Math.Max(half, Math.Min(MinName, nameA)), Math.Max(half, Math.Min(MinName, nameB)));
    }
}
