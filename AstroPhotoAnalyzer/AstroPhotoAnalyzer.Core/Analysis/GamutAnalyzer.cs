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

// GamutAnalyzer.cs - how many pixels leave the paper's gamut (R-144, ADR 0032).
//
// A FIGURE, not a correction: it goes into no curve. A colour is "outside" when the round trip
// sRGB -> printer -> back, through the profile's relative colorimetric tables, moves its a*, b*
// by more than CurveGamutDeltaAb. The chroma only: what falls under paper black is the Tonuri
// tab's business, and without black point compensation the L* of the shadows would be counted
// twice (on canvas, half of the image).
//
// The package (Wacton.Unicolour) makes the round trip in ~28 microseconds per colour (measured on
// 2026-10-05, on the lab's profiles, a 17^3 grid in a fresh process; warm, about 8 microseconds):
// a subsample of 1.4 MP would take 40 s (about 14 s warm). That is why a grid of
// CurveGamutGrid^3 colours is run once, and the pixels take the displacement (da, db) by
// trilinear interpolation. Near the gamut's edge the interpolation rounds the corner, and it
// misses colours rather than adds them: against the exact round trip per pixel (2026-10-23) the
// plate-solved FITS on the lab's baryta reads 1.02 % instead of 1.35 % (the dust class 2.7 %
// instead of 4.2 %), and on its canvas 35.1 % instead of 39.7 %. The figure is an estimate, low
// by up to a few points per class, not a measurement per pixel.

using System;
using System.IO;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The displacement (da, db) of each node of the sRGB grid after the round trip through the
    /// profile.
    /// </summary>
    internal sealed class GamutTable
    {
        internal int N;
        internal float[] Da, Db;

        /// <summary>Why it could not be built, for the user; null when it could.</summary>
        internal string Problem;
    }

    /// <summary>
    /// The share of an image's pixels whose colour leaves the gamut of an ICC printer profile
    /// (R-144).
    /// </summary>
    internal static class GamutAnalyzer
    {
        /// <summary>
        /// The round-trip table of the profile at <paramref name="path"/>; with Problem set when
        /// the profile cannot be used.
        /// </summary>
        /// <param name="path">The ICC printer profile's file.</param>
        /// <param name="n">
        /// The grid's nodes per channel, at least 2; by default CurveGamutGrid.
        /// </param>
        /// <returns>
        /// The table; or, when the file cannot be read, is not a usable profile or has no table
        /// toward the printer (B2A), a table with only Problem set. It never throws.
        /// </returns>
        internal static GamutTable Build(string path, int n = Thresholds.CurveGamutGrid)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var icc = new IccConfiguration(new Profile(bytes, path), Intent.RelativeColorimetric, path);
                if (!string.IsNullOrEmpty(icc.Error)) return new GamutTable { Problem = "profilul nu se poate citi: " + icc.Error };
                var config = new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: icc);
                var t = new GamutTable { N = n, Da = new float[n * n * n], Db = new float[n * n * n] };
                for (int r = 0; r < n; r++)
                    for (int g = 0; g < n; g++)
                        for (int b = 0; b < n; b++)
                        {
                            var colour = new Unicolour(config, ColourSpace.Rgb, r / (n - 1.0), g / (n - 1.0), b / (n - 1.0));
                            var back = new Unicolour(config, colour.Icc);
                            int i = (r * n + g) * n + b;
                            t.Da[i] = (float)(back.Lab.A - colour.Lab.A);
                            t.Db[i] = (float)(back.Lab.B - colour.Lab.B);
                        }
                foreach (float v in t.Da) if (float.IsNaN(v)) return new GamutTable { Problem = "profilul nu are tabela spre imprimanta (B2A)" };
                return t;
            }
            catch (Exception e)
            {
                return new GamutTable { Problem = "profilul nu se poate citi: " + e.Message };
            }
        }

        /// <summary>
        /// The pixel (r, g, b) leaves the gamut: the interpolated displacement passes the
        /// threshold.
        /// </summary>
        /// <param name="t">A table Build made, without a Problem.</param>
        /// <param name="r">The encoded sRGB red, 0..1; clamped to that range.</param>
        /// <param name="g">The encoded sRGB green, 0..1; clamped to that range.</param>
        /// <param name="b">The encoded sRGB blue, 0..1; clamped to that range.</param>
        /// <returns>
        /// <c>true</c> when the displacement moves a*, b* by more than CurveGamutDeltaAb.
        /// </returns>
        internal static bool Outside(GamutTable t, float r, float g, float b)
        {
            int n = t.N;
            float fr = Math.Clamp(r, 0f, 1f) * (n - 1), fg = Math.Clamp(g, 0f, 1f) * (n - 1), fb = Math.Clamp(b, 0f, 1f) * (n - 1);
            int ir = Math.Min((int)fr, n - 2), ig = Math.Min((int)fg, n - 2), ib = Math.Min((int)fb, n - 2);
            float tr = fr - ir, tg = fg - ig, tb = fb - ib;
            float da = 0, db = 0;
            for (int k = 0; k < 8; k++)
            {
                int dr = k & 1, dg = (k >> 1) & 1, dbb = (k >> 2) & 1;
                float w = (dr == 1 ? tr : 1 - tr) * (dg == 1 ? tg : 1 - tg) * (dbb == 1 ? tb : 1 - tb);
                int i = ((ir + dr) * n + ig + dg) * n + ib + dbb;
                da += w * t.Da[i];
                db += w * t.Db[i];
            }
            return da * da + db * db > Thresholds.CurveGamutDeltaAb * Thresholds.CurveGamutDeltaAb;
        }

        /// <summary>
        /// The percentage of the pixels of the tonal subsample <paramref name="sub"/> outside the
        /// gamut, in all and over the five classes of the map; a mono image has no chroma and
        /// gives 0.
        /// </summary>
        /// <param name="t">A table Build made, without a Problem.</param>
        /// <param name="sub">The tonal subsample, 0..1, on the map's grid.</param>
        /// <param name="map">The map, for the class of the patch each pixel lies in.</param>
        /// <returns>
        /// The figure: in all, over every pixel; per class, over the pixels of the patches of that
        /// class, NaN on colour for a class no pixel falls in.
        /// </returns>
        internal static GamutFigure Measure(GamutTable t, Raster sub, DustMap map)
        {
            long all = 0, outAll = 0;
            var n = new long[6]; var o = new long[6];
            if (sub.IsColor)
            {
                float[] R = sub.Planes[0], G = sub.Planes[1], B = sub.Planes[2];
                for (int y = 0; y < sub.H; y++)
                {
                    int by = y / map.Side;
                    for (int x = 0; x < sub.W; x++)
                    {
                        int i = y * sub.W + x;
                        bool outside = Outside(t, R[i], G[i], B[i]);
                        all++; if (outside) outAll++;
                        int bx = x / map.Side;
                        if (bx >= map.Cols || by >= map.Rows) continue;
                        int c = (int)map.Classes[by * map.Cols + bx];
                        n[c]++; if (outside) o[c]++;
                    }
                }
            }
            var per = new double[5];
            for (int c = 1; c <= 5; c++) per[c - 1] = n[c] == 0 ? (sub.IsColor ? double.NaN : 0) : 100.0 * o[c] / n[c];
            return new GamutFigure { PctAll = all == 0 ? 0 : 100.0 * outAll / all, PctByClass = per };
        }
    }
}
