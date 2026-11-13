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
/// One pixel of the pair's canvas, by its whole coordinates: what the read-out names and reads.
/// Whole numbers, not a <see cref="PointD"/>, so that the step from a point to the pixel that
/// holds it is taken once, in <see cref="Viewport.PixelAt"/>, and nowhere after it.
/// </summary>
/// <param name="X">The pixel's column, 0 at the canvas's left edge.</param>
/// <param name="Y">The pixel's row, 0 at the canvas's top edge.</param>
public readonly record struct ImagePixel(int X, int Y);
