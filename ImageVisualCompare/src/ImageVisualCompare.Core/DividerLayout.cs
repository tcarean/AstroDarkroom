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
/// Where every piece of the divider goes, for one viewport size. All of it is in
/// device-independent pixels, because the divider is a fraction of the window and never
/// touches image coordinates.
/// </summary>
/// <param name="LineX">Centre of the line.</param>
/// <param name="ClipRect">The region of the surface in which image A is shown.</param>
/// <param name="Handle">The grab handle, centred vertically on the line.</param>
public readonly record struct DividerLayout(
    double LineX,
    RectD ClipRect,
    RectD Handle);
