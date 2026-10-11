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

namespace DarksCheck.Core;

/// <summary>
/// Every constant a verdict depends on, in one place, so a change is one edit and the design
/// notes can point at it. Each constant carries the one-line fact that set its value; the
/// measurements behind the facts are in <c>docs/validation/</c> and the ADRs.
/// </summary>
internal static class Th
{
    /// <summary>
    /// A pause between the end of one exposure and the start of the next longer than this is
    /// a gap in the series (R-006): back-to-back frames on a DSLR pause under a second; a
    /// longer one means Live View, a menu, or the camera left alone, and the sensor cooled.
    /// </summary>
    internal const double PauseSeconds = 5;

    /// <summary>
    /// Two exposures within this fraction of each other count as the same (R-007): the
    /// D3300 writes 119.2 s for a 120 s setting and 118.5 s once in a while.
    /// </summary>
    internal const double ExposureTolerance = 0.02;

    /// <summary>A bias longer than this is not a bias (R-007); the dark current in it would show.</summary>
    internal const double BiasMaxExposureSeconds = 1.0;
}
