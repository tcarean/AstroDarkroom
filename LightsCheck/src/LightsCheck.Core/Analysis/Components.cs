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

namespace LightsCheck.Core;

/// <summary>
/// One connected component of the detection mask: its pixel count, its bounding box, the
/// highest value and the sum of the background-subtracted raster over it, the highest value
/// of the raster above black (for the saturation test), and its flux-weighted centroid.
/// </summary>
internal sealed class Component
{
    internal int Area;
    internal int Y0 = int.MaxValue, Y1 = -1, X0 = int.MaxValue, X1 = -1;
    internal float Peak = float.MinValue;
    internal float PeakRaw = float.MinValue;
    internal double Flux;
    internal double WeightedY, WeightedX;

    internal int Height => Y1 - Y0 + 1;
    internal int Width => X1 - X0 + 1;
    internal double CentroidY => Flux > 0 ? WeightedY / Flux : (Y0 + Y1) / 2.0;
    internal double CentroidX => Flux > 0 ? WeightedX / Flux : (X0 + X1) / 2.0;
}

/// <summary>
/// Connected components of a threshold mask, 4-connected (scipy's default structure), in
/// two passes with union-find: labels are never materialised as a full raster of ints twice.
/// </summary>
internal static class Components
{
    /// <summary>
    /// Labels the pixels where <paramref name="mask"/> exceeds <paramref name="threshold"/>
    /// and measures each component on <paramref name="values"/> (background-subtracted) and
    /// <paramref name="raw"/> (above black, for the saturation test; may be the same raster).
    /// </summary>
    /// <param name="mask">
    /// The raster thresholded, the smoothed residual in the frame analysis. Not null.
    /// </param>
    /// <param name="threshold">
    /// The level a pixel must exceed, strictly, in the units of <paramref name="mask"/>.
    /// </param>
    /// <param name="values">
    /// The background-subtracted raster the peak, flux and centroid are read on; the size of
    /// <paramref name="mask"/>. Not null.
    /// </param>
    /// <param name="raw">
    /// The raster above black whose highest value per component is kept as
    /// <see cref="Component.PeakRaw"/>; the size of <paramref name="mask"/>. Not null.
    /// </param>
    /// <returns>
    /// The components, in the raster order of their first pixel (top to bottom, left to right);
    /// empty when no pixel passes.
    /// </returns>
    internal static List<Component> Find(Raster mask, double threshold, Raster values, Raster raw)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(raw);
        int h = mask.H, w = mask.W;
        float thr = (float)threshold;
        int[] labels = new int[h * w];
        List<int> parent = [0];

        // First pass: provisional labels, unions where a pixel touches two.
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int i = row + x;
                if (mask.D[i] <= thr)
                {
                    continue;
                }

                int up = y > 0 ? labels[i - w] : 0;
                int left = x > 0 ? labels[i - 1] : 0;
                if (up == 0 && left == 0)
                {
                    parent.Add(parent.Count);
                    labels[i] = parent.Count - 1;
                }
                else if (up == 0 || left == 0 || up == left)
                {
                    labels[i] = up == 0 ? left : up;
                }
                else
                {
                    int a = FindRoot(parent, up), b = FindRoot(parent, left);
                    labels[i] = Math.Min(a, b);
                    if (a != b)
                    {
                        parent[Math.Max(a, b)] = Math.Min(a, b);
                    }
                }
            }
        }

        // Roots to dense indices.
        int[] dense = new int[parent.Count];
        int count = 0;
        for (int l = 1; l < parent.Count; l++)
        {
            if (FindRoot(parent, l) == l)
            {
                dense[l] = ++count;
            }
        }

        Component[] comps = new Component[count + 1];
        for (int c = 1; c <= count; c++)
        {
            comps[c] = new Component();
        }

        // Second pass: measure.
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int l = labels[row + x];
                if (l == 0)
                {
                    continue;
                }

                Component c = comps[dense[FindRoot(parent, l)]];
                float v = values.D[row + x];
                c.Area++;
                if (y < c.Y0)
                {
                    c.Y0 = y;
                }

                if (y > c.Y1)
                {
                    c.Y1 = y;
                }

                if (x < c.X0)
                {
                    c.X0 = x;
                }

                if (x > c.X1)
                {
                    c.X1 = x;
                }

                if (v > c.Peak)
                {
                    c.Peak = v;
                }

                float r = raw.D[row + x];
                if (r > c.PeakRaw)
                {
                    c.PeakRaw = r;
                }

                c.Flux += v;
                c.WeightedY += v * y;
                c.WeightedX += v * x;
            }
        }

        List<Component> result = new(count);
        for (int c = 1; c <= count; c++)
        {
            result.Add(comps[c]);
        }

        return result;
    }

    /// <summary>
    /// The root of a provisional label, halving the path on the way (each visited label points to
    /// its grandparent).
    /// </summary>
    /// <param name="parent">
    /// The union-find table: each label's parent, a root its own. Modified.
    /// </param>
    /// <param name="i">The label, 1 or more.</param>
    /// <returns>The root label of <paramref name="i"/>'s set.</returns>
    private static int FindRoot(List<int> parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }
}
