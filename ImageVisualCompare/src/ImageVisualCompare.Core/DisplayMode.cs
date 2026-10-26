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
/// How the samples of a file shown as stored become the pixels on screen (R-110, ADR 0040).
/// A file WIC converts is always <see cref="Linear"/>.
/// </summary>
public enum DisplayMode
{
    /// <summary>The level of each sample, with no stretch (R-015, R-091).</summary>
    Linear,

    /// <summary>
    /// Each channel equalised on its own histogram, as Siril's "Histogram" display (R-111).
    /// </summary>
    Histogram,

    /// <summary>
    /// One layer equalised and shown through Siril's rainbow colour map (R-112).
    /// </summary>
    FalseColours,
}
