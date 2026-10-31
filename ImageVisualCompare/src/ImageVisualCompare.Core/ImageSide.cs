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
/// Which image of the pair: A, shown left of the divider, or B, shown right of it. Names the
/// image a dialog asks for and the one a change replaces (R-009, R-065).
/// </summary>
public enum ImageSide
{
    /// <summary>Image A, left of the divider.</summary>
    A,

    /// <summary>Image B, right of the divider.</summary>
    B,
}
