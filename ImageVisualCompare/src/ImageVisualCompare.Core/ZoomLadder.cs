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
/// The discrete zoom stops used by the -/+ buttons and keyboard shortcuts. The mouse
/// wheel zooms continuously instead, so an arbitrary scale can be current at any time;
/// the ladder therefore steps to the neighbouring stop rather than to a fixed index.
/// </summary>
public static class ZoomLadder
{
    private const double Epsilon = 1e-6;

    public static IReadOnlyList<double> Steps { get; } =
    [
        0.02, 0.03, 0.05, 0.08, 0.125, 0.17, 0.25, 0.33, 0.5, 0.67,
        1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0, 12.0, 16.0
    ];

    /// <summary>Finds the stop to zoom in to from <paramref name="scale"/>.</summary>
    /// <param name="scale">
    /// The current scale, 1 being one image pixel per physical screen pixel: a stop, a value
    /// between two after a wheel zoom, or one past either end of the ladder.
    /// </param>
    /// <returns>
    /// The lowest stop strictly above <paramref name="scale"/>, or the highest stop when none is,
    /// which for a scale above the ladder is a step down.
    /// </returns>
    public static double Next(double scale)
    {
        foreach (double step in Steps)
        {
            if (step > scale + Epsilon)
            {
                return step;
            }
        }

        return Steps[^1];
    }

    /// <summary>Finds the stop to zoom out to from <paramref name="scale"/>.</summary>
    /// <param name="scale">
    /// The current scale, 1 being one image pixel per physical screen pixel: a stop, a value
    /// between two after a wheel zoom, or one past either end of the ladder, such as a fit
    /// scale under 2%.
    /// </param>
    /// <returns>
    /// The highest stop strictly below <paramref name="scale"/>, or the lowest stop when none
    /// is, which for a scale under the ladder is a step up; <see cref="Viewport.ZoomOut"/> does
    /// not take that step, and goes to its floor instead.
    /// </returns>
    public static double Previous(double scale)
    {
        for (int i = Steps.Count - 1; i >= 0; i--)
        {
            if (Steps[i] < scale - Epsilon)
            {
                return Steps[i];
            }
        }

        return Steps[0];
    }
}
