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

// RasterOps.cs - the pure transformations of a Raster: subsampling, cropping, the gamma curve,
// quantization, L* over the whole plane, and the guard against linear data.
//
// Those that return a raster return a new one, except Subsample with a stride of 1 or less,
// which returns its input; none modifies its input. The analyzers can therefore work on the same
// raster in any order.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The operations on rasters and samples that the measurements share; none modifies its
    /// input.
    /// </summary>
    internal static class RasterOps
    {
        /// <summary>
        /// Every <paramref name="stride"/>-th pixel on both axes, anchored at the top left -
        /// exactly numpy's a[::s, ::s], with which the acceptance figures were measured. Not a
        /// block average: the tonal distributions want real pixels, not smoothed ones.
        /// </summary>
        /// <param name="r">The raster.</param>
        /// <param name="stride">
        /// The step, in pixels; 1 or less returns <paramref name="r"/> itself.
        /// </param>
        /// <returns>
        /// A raster of ceil(H / stride) rows of ceil(W / stride) pixels, with the same planes and
        /// <see cref="Raster.Source"/>.
        /// </returns>
        internal static Raster Subsample(Raster r, int stride)
        {
            if (stride <= 1) return r;
            int h = (r.H + stride - 1) / stride, w = (r.W + stride - 1) / stride;
            var o = new Raster(h, w, r.Planes.Length) { Source = r.Source };
            for (int p = 0; p < r.Planes.Length; p++)
            {
                float[] src = r.Planes[p], dst = o.Planes[p];
                for (int y = 0; y < h; y++)
                {
                    int sy = y * stride * r.W;
                    int dy = y * w;
                    for (int x = 0; x < w; x++) dst[dy + x] = src[sy + x * stride];
                }
            }
            return o;
        }

        /// <summary>
        /// v^gamma on every channel, the same on all - so a neutral grey stays neutral. It is the
        /// print simulation's curve: gamma = ln(target)/ln(background) takes the measured
        /// background exactly to the target and lifts the rest in proportion. Not a real
        /// stretch, and the report calls it a simulation.
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="gamma">The exponent, positive, as <see cref="GammaFor"/> gives it.</param>
        /// <returns>A new raster, every sample raised to <paramref name="gamma"/>.</returns>
        internal static Raster Gamma(Raster r, double gamma)
        {
            // Through a table on the 65536 levels of 16 bits, not through Math.Pow on every
            // sample: on a raster of 4 million samples Pow would take seconds. On a 16-bit source
            // the value falls on a level of the table to within float rounding and the result is
            // Pow's to float precision; on float (FITS) it is interpolated linearly between the
            // two neighbouring levels, with an error under 1e-6 above a sample of about 0.01 or
            // for a gamma of about 0.93 or more, and up to 0.015 in the first 16-bit step at
            // gamma 0.3. The print simulation no longer
            // goes through here: it lifts and linearises from a single table (LiftTable,
            // ADR 0035); what stays here is the preview and the window's map, which need the
            // lifted raster.
            double[] lut = PowerLut(gamma);
            var o = new Raster(r.H, r.W, r.Planes.Length) { Source = r.Source };
            for (int p = 0; p < r.Planes.Length; p++)
            {
                float[] src = r.Planes[p], dst = o.Planes[p];
                for (int i = 0; i < src.Length; i++) dst[i] = (float)Interpolate(lut, src[i]);
            }
            return o;
        }

        /// <summary>
        /// The value of a table on the 65536 levels of 16 bits (PowerLut, LiftTable) at the
        /// sample <paramref name="s"/>: exact on a level, linear between two neighbouring levels.
        /// A NaN is taken as 0: it would give (int)NaN = int.MinValue, an impossible index; the
        /// readers do not let NaN through (R-013), but a raster made in memory can.
        /// </summary>
        /// <param name="lut">
        /// The table: 65537 entries, the value at the level k / 65535 at index k.
        /// </param>
        /// <param name="s">The sample, 0..1; clamped to that range.</param>
        /// <returns>The table's value at the sample.</returns>
        internal static double Interpolate(double[] lut, float s)
        {
            double v = (float.IsNaN(s) ? 0 : Math.Clamp(s, 0f, 1f)) * 65535.0;
            int k = (int)v;
            double f = v - k;
            return f == 0 ? lut[k] : lut[k] + f * (lut[k + 1] - lut[k]);
        }

        /// <summary>
        /// The table of the curve v^gamma on the 65536 levels of 16 bits, plus an entry at level
        /// 65536 that Interpolate never reads: the same for Gamma and for LiftTable.
        /// </summary>
        /// <param name="gamma">The exponent.</param>
        /// <returns>65537 entries, (k / 65535)^gamma at index k.</returns>
        internal static double[] PowerLut(double gamma)
        {
            var lut = new double[65537];
            for (int k = 0; k < lut.Length; k++) lut[k] = Math.Pow(k / 65535.0, gamma);
            return lut;
        }

        /// <summary>
        /// The gamma that takes the background <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        /// <param name="from">The measured background level, 0..1, both ends excluded.</param>
        /// <param name="to">The target level, 0..1, both ends excluded.</param>
        /// <returns>ln(to) / ln(from): under 1 lifts, over 1 lowers.</returns>
        internal static double GammaFor(double from, double to)
        {
            if (from <= 0 || from >= 1 || to <= 0 || to >= 1)
                throw new ArgumentException(Tr.Pick("the background and the target must be in (0, 1)", "fundalul si tinta trebuie sa fie in (0, 1)"));
            return Math.Log(to) / Math.Log(from);
        }

        /// <summary>The 8-bit level, by rounding - what an 8-bit export would write.</summary>
        /// <param name="v">The sample; clamped to 0..1.</param>
        /// <returns>The level, 0..255.</returns>
        internal static int Level8(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 255.0);

        /// <summary>
        /// The 16-bit level. On a 16-bit source it returns exactly what was in the file.
        /// </summary>
        /// <param name="v">The sample; clamped to 0..1.</param>
        /// <returns>The level, 0..65535.</returns>
        internal static int Level16(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 65535.0);

        /// <summary>
        /// L* for every pixel, on the raster subsampled with <paramref name="stride"/>. On colour
        /// it goes through the Rec. 709 luminance of the linearised components; on mono it is the
        /// L* of the value. The vector is in pixel order, ready to sort.
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="stride">The subsampling step, as in <see cref="Subsample"/>.</param>
        /// <returns>One L* (0..100) per pixel of the subsample.</returns>
        internal static float[] LstarPlane(Raster r, int stride)
            => LstarPlane(Subsample(r, stride), new ExactLinearizer());

        /// <summary>
        /// L* for every pixel of an ALREADY subsampled raster, with the samples passed through
        /// <paramref name="lin"/>: exact, or lifted with a gamma and linearised from the table
        /// (LiftTable), as the simulation wants them on the rows of its grid.
        /// </summary>
        /// <typeparam name="T">The path, a struct the JIT specialises the loop on.</typeparam>
        /// <param name="s">The subsampled raster, 0..1.</param>
        /// <param name="lin">
        /// The path: an <see cref="ExactLinearizer"/> or a <see cref="LiftTable"/>.
        /// </param>
        /// <returns>One L* (0..100) per pixel, in pixel order.</returns>
        internal static float[] LstarPlane<T>(Raster s, T lin) where T : struct, ISrgbLinearizer
        {
            var o = new float[s.H * s.W];
            if (s.IsColor)
            {
                float[] R = s.Planes[0], G = s.Planes[1], B = s.Planes[2];
                for (int i = 0; i < o.Length; i++)
                    o[i] = (float)ColorMath.LstarOfY(ColorMath.Luminance(lin.Linear(R[i]), lin.Linear(G[i]), lin.Linear(B[i])));
            }
            else
            {
                float[] M = s.Planes[0];
                for (int i = 0; i < o.Length; i++)
                    o[i] = (float)ColorMath.LstarOfY(lin.Linear(M[i]));
            }
            return o;
        }

        /// <summary>
        /// Does the image look LINEAR, not stretched? A linear stack has its median at about a
        /// thousandth, almost everything in the first percents of the scale; a finished image has
        /// its background at 0.1-0.25. The threshold is
        /// <see cref="Thresholds.LinearMedianMax"/>. The guard exists so that the tool refuses
        /// with a message, rather than report "99% sub negrul hartiei" on a file nobody prints
        /// like that.
        /// </summary>
        /// <param name="r">The raster, 0..1.</param>
        /// <param name="median">
        /// The median of the reference plane, subsampled at stride 8, 0..1.
        /// </param>
        /// <returns><c>true</c> when the median is under the threshold.</returns>
        internal static bool LooksLinear(Raster r, out double median)
        {
            median = Stats.Median(Subsample(r, 8).Reference);
            return median < Thresholds.LinearMedianMax;
        }
    }
}
