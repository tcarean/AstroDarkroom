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
/// One measured star (R-014): its core, read by adaptive moments (centre, flux, width, the two
/// axis widths and their ratio), and the whole star, tails and second copies included (the ratio
/// of the major to the minor axis and the major axis's angle), with the peak and area of the
/// component it came from.
/// </summary>
/// <param name="X">The core's centre column, in pixels.</param>
/// <param name="Y">The core's centre row, in pixels.</param>
/// <param name="Flux">
/// The core's flux, in ADU: twice the sum under the matched weight, the whole flux of a Gaussian.
/// </param>
/// <param name="Fwhm">
/// The core's width: 2.3548 times the root of the mean of its two second moments, in pixels.
/// </param>
/// <param name="Elongation">
/// The whole star's elongation: the root of the ratio of the major to the minor second moment of
/// the pixels above the local noise, 1 or more.
/// </param>
/// <param name="AngleDeg">
/// The whole star's major axis, from the x axis (x right, y down), in degrees (-90, 90].
/// </param>
/// <param name="CoreElongation">
/// The core's elongation: the root of the ratio of its major to its minor second moment, 1 or
/// more. Close to <paramref name="Elongation"/> on a star stretched as a whole, and well under
/// it on a sharp star with a tail or a second copy.
/// </param>
/// <param name="FwhmMajor">The core's FWHM along its major axis, in pixels.</param>
/// <param name="FwhmMinor">The core's FWHM along its minor axis, in pixels.</param>
/// <param name="Peak">The component's highest background-subtracted value, in ADU.</param>
/// <param name="Area">The component's pixel count.</param>
internal sealed record StarShape(
    double X,
    double Y,
    double Flux,
    double Fwhm,
    double Elongation,
    double AngleDeg,
    double CoreElongation,
    double FwhmMajor,
    double FwhmMinor,
    double Peak,
    int Area);

/// <summary>
/// The shape and the halo of a star on the background-subtracted raster. The shape follows the
/// star, whatever its size in pixels (docs/design/frame-analysis.md, ADR 0056).
/// </summary>
internal static class StarShapes
{
    /// <summary>The FWHM of a Gaussian over its sigma.</summary>
    private const double FwhmOverSigma = 2.3548;

    /// <summary>
    /// A star's core: its centre, its flux and its second moments, the weight's at the round it
    /// agreed with the star.
    /// </summary>
    /// <param name="Y">The centre's row, in pixels.</param>
    /// <param name="X">The centre's column, in pixels.</param>
    /// <param name="Flux">Twice the sum under the weight, in ADU.</param>
    /// <param name="Xx">The second moment along x, in square pixels.</param>
    /// <param name="Yy">The second moment along y, in square pixels.</param>
    /// <param name="Xy">The mixed second moment, in square pixels.</param>
    private readonly record struct CoreMoments(double Y, double X, double Flux, double Xx, double Yy, double Xy);

    /// <summary>
    /// Measures one star: the core by adaptive moments, then the whole star by the moments of
    /// the pixels above the local noise inside a circle of <see cref="Th.WholeStarRadiusWidths"/>
    /// core widths (R-014).
    /// </summary>
    /// <param name="d">The background-subtracted raster, in ADU. Not null.</param>
    /// <param name="cy">The component's centroid row, in pixels: where the core starts.</param>
    /// <param name="cx">The component's centroid column, in pixels.</param>
    /// <param name="peak">The component's peak, carried into the result.</param>
    /// <param name="area">
    /// The component's pixel count, 1 or more: the core's first width is read off it, and it is
    /// carried into the result.
    /// </param>
    /// <returns>
    /// The star's shape; null when the core's weight does not settle on the star (it runs away
    /// into the sky, slides off the component or finds no flux) or a window leaves the raster.
    /// </returns>
    internal static StarShape? Measure(Raster d, double cy, double cx, double peak, int area)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Core(d, cy, cx, area) is not CoreMoments core || Axes(core.Xx, core.Yy, core.Xy) is not (double major, double minor))
        {
            return null;
        }

        double fwhm = FwhmOverSigma * Math.Sqrt((major + minor) / 2);
        if (Whole(d, core.Y, core.X, fwhm) is not (double elongation, double angle))
        {
            return null;
        }

        return new StarShape(core.X, core.Y, core.Flux, fwhm, elongation, angle, Math.Sqrt(major / minor),
            FwhmOverSigma * Math.Sqrt(major), FwhmOverSigma * Math.Sqrt(minor), peak, area);
    }

    /// <summary>
    /// The star's core by adaptive moments: the second moments under a Gaussian weight, the
    /// weight then set to the star those moments describe (its inverse covariance is the
    /// moments' less the weight's) and the centre moved by twice the weighted offset, until a
    /// round changes neither. For a Gaussian star the weight ends as the star itself, whatever
    /// its size, and the sky's noise far from it has no weight.
    /// </summary>
    /// <param name="d">The background-subtracted raster, in ADU.</param>
    /// <param name="cy">The starting centre's row, in pixels.</param>
    /// <param name="cx">The starting centre's column, in pixels.</param>
    /// <param name="area">The component's pixel count: the first weight's size.</param>
    /// <returns>
    /// The core; null when the weight does not settle within <see cref="Th.CoreIterations"/>
    /// rounds, grows past <see cref="Th.CoreDivergedFwhm"/>, leaves the raster, holds no flux,
    /// or its centre leaves the start by more than the first width.
    /// </returns>
    private static CoreMoments? Core(Raster d, double cy, double cx, int area)
    {
        double startY = cy, startX = cx;
        double firstFwhm = Math.Clamp(FwhmOverSigma * Math.Sqrt(area / Math.PI) / Th.CoreStartDivisor, Th.CoreStartMinFwhm, Th.CoreStartMaxFwhm);
        double firstSigma = firstFwhm / FwhmOverSigma;
        double wxx = firstSigma * firstSigma, wyy = wxx, wxy = 0;
        double drift = Math.Max(Th.CoreCentreDriftMin, firstFwhm);
        double reach2 = Th.CoreWeightReach * Th.CoreWeightReach;
        for (int it = 0; it < Th.CoreIterations; it++)
        {
            double tr = wxx + wyy, det = wxx * wyy - wxy * wxy;
            if (!(det > 0))
            {
                return null;
            }

            double largest = tr / 2 + Math.Sqrt(Math.Max(tr * tr / 4 - det, 0));
            if (FwhmOverSigma * Math.Sqrt(largest) > Th.CoreDivergedFwhm)
            {
                return null;
            }

            int half = (int)Math.Ceiling(Th.CoreWeightReach * Math.Sqrt(largest)) + 1;
            int iy = (int)Math.Round(cy), ix = (int)Math.Round(cx);
            int y0 = iy - half, y1 = iy + half + 1, x0 = ix - half, x1 = ix + half + 1;
            if (y0 < 0 || x0 < 0 || y1 > d.H || x1 > d.W)
            {
                return null;
            }

            // The weight's inverse covariance.
            double ixx = wyy / det, iyy = wxx / det, ixy = -wxy / det;
            double s = 0, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            for (int y = y0; y < y1; y++)
            {
                double dy = y - cy;
                int row = y * d.W;
                for (int x = x0; x < x1; x++)
                {
                    double dx = x - cx;
                    double q = ixx * dx * dx + 2 * ixy * dx * dy + iyy * dy * dy;
                    if (q > reach2)
                    {
                        continue;
                    }

                    double v = d.D[row + x] * Math.Exp(-0.5 * q);
                    s += v;
                    sx += v * dx;
                    sy += v * dy;
                    sxx += v * dx * dx;
                    syy += v * dy * dy;
                    sxy += v * dx * dy;
                }
            }

            if (!(s > 0))
            {
                return null;
            }

            double mx = sx / s, my = sy / s;
            double mxx = sxx / s - mx * mx, myy = syy / s - my * my, mxy = sxy / s - mx * my;
            double dm = mxx * myy - mxy * mxy;
            if (!(dm > 0) || !(mxx > 0))
            {
                return null;
            }

            // Under a Gaussian weight a Gaussian star's moments have the inverse covariance of
            // the star plus the weight's, so the star's is the moments' less the weight's. When
            // that is no covariance (the star is no Gaussian, or the noise), twice the moments,
            // the same at the fixed point.
            double cxx = myy / dm - ixx, cyy = mxx / dm - iyy, cxy = -mxy / dm - ixy;
            double dc = cxx * cyy - cxy * cxy;
            double nxx, nyy, nxy;
            if (cxx > 0 && cyy > 0 && dc > 0)
            {
                nxx = cyy / dc;
                nyy = cxx / dc;
                nxy = -cxy / dc;
            }
            else
            {
                nxx = 2 * mxx;
                nyy = 2 * myy;
                nxy = 2 * mxy;
            }

            double ntr = nxx + nyy;
            if (ntr > Th.CoreGrowthLimit * tr)
            {
                double k = Th.CoreGrowthLimit * tr / ntr;
                nxx *= k;
                nyy *= k;
                nxy *= k;
            }

            // Under the matched weight the weighted centre sits halfway to the star's.
            double cap = Math.Sqrt(largest);
            double stepY = Math.Clamp(2 * my, -cap, cap), stepX = Math.Clamp(2 * mx, -cap, cap);
            cy += stepY;
            cx += stepX;
            if (Math.Abs(cy - startY) > drift || Math.Abs(cx - startX) > drift)
            {
                return null;
            }

            double change = (Math.Abs(nxx - wxx) + Math.Abs(nyy - wyy) + 2 * Math.Abs(nxy - wxy)) / tr;
            wxx = nxx;
            wyy = nyy;
            wxy = nxy;
            if (change < Th.CoreTolerance && Math.Abs(stepX) + Math.Abs(stepY) < Th.CoreCentreTolerance)
            {
                return new CoreMoments(cy, cx, 2 * s, wxx, wyy, wxy);
            }
        }

        return null;
    }

    /// <summary>
    /// The whole star's elongation and angle: the second moments of the pixels at least
    /// <see cref="Th.WholeStarSigmas"/> local sigmas above the local sky, inside a circle of
    /// <see cref="Th.WholeStarRadiusWidths"/> core widths around a centre refined
    /// <see cref="Th.WholeStarCentreIterations"/> times. A tail or a second copy counts, the
    /// sky's noise does not: no pixel of it passes the cut but by chance.
    /// </summary>
    /// <param name="d">The background-subtracted raster, in ADU.</param>
    /// <param name="cy">The core's centre row, in pixels.</param>
    /// <param name="cx">The core's centre column, in pixels.</param>
    /// <param name="fwhm">The core's width, in pixels: the circle's radius follows it.</param>
    /// <returns>
    /// The ratio of the major to the minor axis and the major axis's angle from the x axis, in
    /// degrees; null when the circle or its ring leaves the raster, or no pixel passes the cut.
    /// </returns>
    private static (double Elongation, double AngleDeg)? Whole(Raster d, double cy, double cx, double fwhm)
    {
        double radius = Math.Max(Th.WholeStarMinRadius, Th.WholeStarRadiusWidths * fwhm);
        double inner = radius + Th.LocalRingGap;
        if (Ring(d, cy, cx, inner, inner + Math.Max(Th.LocalRingMinWidth, Th.LocalRingWidthFraction * radius)) is not (double sky, double sigma))
        {
            return null;
        }

        double cut = Th.WholeStarSigmas * sigma;
        int reach = (int)Math.Ceiling(radius) + 2;
        double r2 = radius * radius;
        for (int it = 0; it <= Th.WholeStarCentreIterations; it++)
        {
            int y0 = (int)cy - reach, y1 = (int)cy + reach + 1, x0 = (int)cx - reach, x1 = (int)cx + reach + 1;
            if (y0 < 0 || x0 < 0 || y1 > d.H || x1 > d.W)
            {
                return null;
            }

            double s = 0, sy = 0, sx = 0, sxx = 0, syy = 0, sxy = 0;
            for (int y = y0; y < y1; y++)
            {
                double dy = y - cy;
                int row = y * d.W;
                for (int x = x0; x < x1; x++)
                {
                    double dx = x - cx;
                    if (dy * dy + dx * dx > r2)
                    {
                        continue;
                    }

                    double v = d.D[row + x] - sky;
                    if (v < cut)
                    {
                        continue;
                    }

                    s += v;
                    sy += v * dy;
                    sx += v * dx;
                    sxx += v * dx * dx;
                    syy += v * dy * dy;
                    sxy += v * dx * dy;
                }
            }

            if (!(s > 0))
            {
                return null;
            }

            double my = sy / s, mx = sx / s;
            if (it < Th.WholeStarCentreIterations)
            {
                cy += my;
                cx += mx;
                continue;
            }

            double mxx = sxx / s - mx * mx, myy = syy / s - my * my, mxy = sxy / s - mx * my;
            return Axes(mxx, myy, mxy) is (double major, double minor)
                ? (Math.Sqrt(major / minor), 0.5 * (180.0 / Math.PI) * Math.Atan2(2 * mxy, mxx - myy))
                : null;
        }

        return null;
    }

    /// <summary>The two eigenvalues of a set of second moments.</summary>
    /// <param name="mxx">The second moment along x.</param>
    /// <param name="myy">The second moment along y.</param>
    /// <param name="mxy">The mixed second moment.</param>
    /// <returns>
    /// The major and the minor second moment; null when the minor is not positive or a value is
    /// not a number.
    /// </returns>
    private static (double Major, double Minor)? Axes(double mxx, double myy, double mxy)
    {
        double tr = mxx + myy, det = mxx * myy - mxy * mxy;
        double disc = Math.Sqrt(Math.Max(tr * tr / 4 - det, 0));
        double major = tr / 2 + disc, minor = tr / 2 - disc;
        return minor > 0 && double.IsFinite(major) ? (major, minor) : null;
    }

    /// <summary>
    /// The local sky and its noise around a star: the median of the pixels in a ring and 1.4826
    /// times their median absolute deviation, which the stars that fall in the ring barely move.
    /// </summary>
    /// <param name="d">The background-subtracted raster, in ADU.</param>
    /// <param name="cy">The ring's centre row, in pixels.</param>
    /// <param name="cx">The ring's centre column, in pixels.</param>
    /// <param name="inner">The ring's inner radius, in pixels, excluded.</param>
    /// <param name="outer">The ring's outer radius, in pixels, included.</param>
    /// <returns>The median and the robust sigma, in ADU; null when the ring leaves the raster.</returns>
    private static (double Median, double Sigma)? Ring(Raster d, double cy, double cx, double inner, double outer)
    {
        int r = (int)Math.Ceiling(outer) + 1;
        int iy = (int)Math.Round(cy), ix = (int)Math.Round(cx);
        int y0 = iy - r, y1 = iy + r + 1, x0 = ix - r, x1 = ix + r + 1;
        if (y0 < 0 || x0 < 0 || y1 > d.H || x1 > d.W)
        {
            return null;
        }

        float[] values = new float[(2 * r + 1) * (2 * r + 1)];
        int n = 0;
        double in2 = inner * inner, out2 = outer * outer;
        for (int y = y0; y < y1; y++)
        {
            double dy = y - cy;
            for (int x = x0; x < x1; x++)
            {
                double dx = x - cx;
                double rr = dy * dy + dx * dx;
                if (rr > in2 && rr <= out2)
                {
                    values[n++] = d.D[y * d.W + x];
                }
            }
        }

        if (n == 0)
        {
            return null;
        }

        float median = Raster.Median(values, n);
        for (int i = 0; i < n; i++)
        {
            values[i] = Math.Abs(values[i] - median);
        }

        return (median, 1.4826 * Raster.Median(values, n));
    }

    /// <summary>
    /// The flux in the annulus (<paramref name="inner"/>, <paramref name="outer"/>] over the
    /// flux within <paramref name="core"/>, on the raw (unclipped) background-subtracted
    /// raster. Null when the window leaves the raster or the core holds no flux.
    /// </summary>
    /// <param name="d">The background-subtracted raster, in ADU. Not null.</param>
    /// <param name="cy">The star's centroid row, in pixels.</param>
    /// <param name="cx">The star's centroid column, in pixels.</param>
    /// <param name="core">The core's radius, in pixels, included.</param>
    /// <param name="inner">The annulus's inner radius, in pixels, excluded.</param>
    /// <param name="outer">The annulus's outer radius, in pixels, included.</param>
    /// <returns>The ratio of the annulus flux to the core flux; null as the summary says.</returns>
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
