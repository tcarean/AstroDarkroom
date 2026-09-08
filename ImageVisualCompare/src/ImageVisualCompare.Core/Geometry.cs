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

/// <summary>A width and height, in whatever space the caller is working in.</summary>
public readonly record struct SizeD(double Width, double Height);

/// <summary>
/// An axis-aligned rectangle. Declared here rather than reusing <c>System.Windows.Rect</c>
/// so the layout maths stays free of any UI framework type and testable without a WPF host.
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

public static class AspectFit
{
    /// <summary>
    /// The largest box with the content's aspect ratio that fits inside the given bounds.
    /// Used both for the on-screen minimap and for the thumbnail bitmap behind it, which is
    /// why it is a function rather than being written out twice.
    /// </summary>
    public static SizeD Fit(double contentWidth, double contentHeight, double maxWidth, double maxHeight)
    {
        if (contentWidth <= 0 || contentHeight <= 0 || maxWidth <= 0 || maxHeight <= 0)
        {
            return default;
        }

        double scale = Math.Min(maxWidth / contentWidth, maxHeight / contentHeight);
        return new SizeD(contentWidth * scale, contentHeight * scale);
    }
}
