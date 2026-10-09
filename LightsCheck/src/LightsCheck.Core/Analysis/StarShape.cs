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
/// One measured star: its centroid, its flux inside the moment window, the FWHM a Gaussian
/// of the same second moments would have, the ratio of the major to the minor axis, the
/// angle of the major axis from the x axis in degrees (-90, 90], the two axis FWHMs, and
/// the component's peak and area it came from.
/// </summary>
internal sealed record StarShape(
    double X,
    double Y,
    double Flux,
    double Fwhm,
    double Elongation,
    double AngleDeg,
    double FwhmMajor,
    double FwhmMinor,
    double Peak,
    int Area);

/// <summary>Second-moment shape and halo of a star on the background-subtracted raster.</summary>
internal static class StarShapes
{
    /// <summary>
    /// The moments in a circular window of <paramref name="radius"/> around the centroid,
    /// the centroid refined <paramref name="iterations"/> times, negative values clipped to
    /// zero. Null when the window leaves the raster or holds no flux.
    /// </summary>
    internal static StarShape? Measure(Raster d, double cy, double cx, int radius, int iterations, double peak, int area)
    {
        ArgumentNullException.ThrowIfNull(d);
        int y0 = (int)cy - radius - 2, y1 = (int)cy + radius + 3;
        int x0 = (int)cx - radius - 2, x1 = (int)cx + radius + 3;
        if (y0 < 0 || x0 < 0 || y1 > d.H || x1 > d.W)
        {
            return null;
        }

        double r2 = (double)radius * radius;
        double s = 0;
        for (int it = 0; it < iterations; it++)
        {
            double sy = 0, sx = 0;
            s = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    double dy = y - cy, dx = x - cx;
                    if (dy * dy + dx * dx > r2)
                    {
                        continue;
                    }

                    double v = Math.Max(d.D[y * d.W + x], 0);
                    s += v;
                    sy += v * y;
                    sx += v * x;
                }
            }

            if (s <= 0)
            {
                return null;
            }

            cy = sy / s;
            cx = sx / s;
        }

        double mxx = 0, myy = 0, mxy = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                double dy = y - cy, dx = x - cx;
                if (dy * dy + dx * dx > r2)
                {
                    continue;
                }

                double v = Math.Max(d.D[y * d.W + x], 0);
                mxx += v * dx * dx;
                myy += v * dy * dy;
                mxy += v * dx * dy;
            }
        }

        mxx /= s;
        myy /= s;
        mxy /= s;
        double tr = mxx + myy, det = mxx * myy - mxy * mxy;
        double disc = Math.Max(tr * tr / 4 - det, 0);
        double l1 = tr / 2 + Math.Sqrt(disc), l2 = tr / 2 - Math.Sqrt(disc);
        if (l2 <= 0)
        {
            return null;
        }

        const double Fwhm = 2.3548;
        return new StarShape(cx, cy, s, Fwhm * Math.Sqrt(tr / 2), Math.Sqrt(l1 / l2),
            0.5 * (180.0 / Math.PI) * Math.Atan2(2 * mxy, mxx - myy), Fwhm * Math.Sqrt(l1), Fwhm * Math.Sqrt(l2), peak, area);
    }

    /// <summary>
    /// The flux in the annulus (<paramref name="inner"/>, <paramref name="outer"/>] over the
    /// flux within <paramref name="core"/>, on the raw (unclipped) background-subtracted
    /// raster. Null when the window leaves the raster or the core holds no flux.
    /// </summary>
    internal static double? Halo(Raster d, double cy, double cx, double core, double inner, double outer)
    {
        ArgumentNullException.ThrowIfNull(d);
        int r = (int)Math.Ceiling(outer) + 1;
        int y0 = (int)cy - r, y1 = (int)cy + r + 1, x0 = (int)cx - r, x1 = (int)cx + r + 1;
        if (y0 < 0 || x0 < 0 || y1 > d.H || x1 > d.W)
        {
            return null;
        }

        double coreSum = 0, ring = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                double rr = Math.Sqrt((y - cy) * (y - cy) + (x - cx) * (x - cx));
                double v = d.D[y * d.W + x];
                if (rr <= core)
                {
                    coreSum += v;
                }
                else if (rr > inner && rr <= outer)
                {
                    ring += v;
                }
            }
        }

        return coreSum > 0 ? ring / coreSum : null;
    }
}
