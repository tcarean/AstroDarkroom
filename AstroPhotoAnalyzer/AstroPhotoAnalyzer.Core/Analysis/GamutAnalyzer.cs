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

// GamutAnalyzer.cs - how many pixels leave the paper's gamut (R-144, ADR 0032, ADR 0042).
//
// A FIGURE, not a correction: it goes into no curve. A colour is "outside" when the round trip
// sRGB -> printer -> back, through the profile's relative colorimetric tables, moves its a*, b*
// by more than CurveGamutDeltaAb. The chroma only: what falls under paper black is the Tonuri
// tab's business, and without black point compensation the L* of the shadows would be counted
// twice (on canvas, half of the image).
//
// The package (Wacton.Unicolour) makes the round trip in about 25 microseconds per colour
// (single-threaded, warm; measured 2026-10-23 on the lab's baryta profile): a subsample of
// 1.4 MP would take 35 s per curve. So a grid of CurveGamutGrid^3 colours is run once, and each
// pixel takes the displacement (da, db) by trilinear interpolation. Near the gamut's edge the
// interpolation rounds the corner inward and misses colours rather than adds them: against the
// exact round trip per pixel the grid read the plate-solved FITS on the lab's baryta at 1.02 %
// instead of 1.35 % (its dust class 2.7 instead of 4.2), its canvas at 35.1 % instead of
// 39.7 %, and de_printat.png's dust class at 0.0 % instead of 2.4 %; a grid of 65 nodes was
// still 0.45 and 2.2 points low on the two dust classes.
//
// THE HYBRID (ADR 0042). A pixel whose interpolated displacement lies within
// CurveGamutExactMargin of the threshold takes the exact round trip instead, once per distinct
// 8-bit colour of such pixels (a cache the table keeps for the run), with the rows of the
// subsample measured in parallel; the others keep the grid's answer. Measured on 2026-10-23
// against the exact figure, with the margin at 1.0: every class within 0.17 points on the three
// pairs (the canvas sky 38.24 against 38.08; 0.10 % of the pixels decided otherwise on baryta,
// 1.6 % on canvas, 0.02 % on the PNG); 9 100 trips per measure on the FITS on baryta, 29 000 to
// 40 500 on its canvas, 1 700 on the PNG, where the grid alone (margin 0) failed the 0.2 points
// and the exact trip for every near pixel (no cache) cost 11 s per measure on canvas. The
// package's configuration served eight threads in the probe with the single-threaded results,
// to the pixel.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The displacement (da, db) of each node of the sRGB grid after the round trip through the
    /// profile, the exact round trip for the pixels near the threshold, and the cache of the
    /// trips already made.
    /// </summary>
    internal sealed class GamutTable
    {
        internal int N;
        internal float[] Da, Db;

        /// <summary>
        /// The exact round trip of a colour (encoded sRGB, 0..1 each): the displacement (da, db)
        /// of its a*, b*. Null when no profile stands behind the table (a hand-built table in a
        /// test), and then the interpolation alone decides every pixel.
        /// </summary>
        internal Func<double, double, double, (double Da, double Db)> Exact;

        /// <summary>
        /// The trips already made, by 8-bit colour (r &lt;&lt; 16 | g &lt;&lt; 8 | b): whether the
        /// colour is outside. Shared by every Measure on this table and by the rows measured in
        /// parallel; a colour two threads reach at once is tripped twice, to the same answer.
        /// </summary>
        internal readonly ConcurrentDictionary<int, bool> Cache = new();

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
        /// The round-trip table of the profile at <paramref name="path"/>, with the exact trip
        /// for the pixels near the threshold; with Problem set when the profile cannot be used.
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
                if (!string.IsNullOrEmpty(icc.Error)) return new GamutTable { Problem = Tr.Pick("the profile cannot be read: ", "profilul nu se poate citi: ") + icc.Error };
                var config = new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: icc);
                var t = new GamutTable { N = n, Da = new float[n * n * n], Db = new float[n * n * n] };
                for (int r = 0; r < n; r++)
                    for (int g = 0; g < n; g++)
                        for (int b = 0; b < n; b++)
                        {
                            var (da, db) = RoundTrip(config, r / (n - 1.0), g / (n - 1.0), b / (n - 1.0));
                            int i = (r * n + g) * n + b;
                            t.Da[i] = (float)da;
                            t.Db[i] = (float)db;
                        }
                foreach (float v in t.Da) if (float.IsNaN(v)) return new GamutTable { Problem = Tr.Pick("the profile has no table towards the printer (B2A)", "profilul nu are tabela spre imprimanta (B2A)") };
                t.Exact = (r, g, b) => RoundTrip(config, r, g, b);
                return t;
            }
            catch (Exception e)
            {
                return new GamutTable { Problem = Tr.Pick("the profile cannot be read: ", "profilul nu se poate citi: ") + e.Message };
            }
        }

        /// <summary>
        /// The round trip of one colour through the profile: sRGB to the printer's device values
        /// and back, the displacement of a*, b*.
        /// </summary>
        /// <param name="config">The package's configuration with the profile's tables.</param>
        /// <param name="r">The encoded sRGB red, 0..1.</param>
        /// <param name="g">The encoded sRGB green, 0..1.</param>
        /// <param name="b">The encoded sRGB blue, 0..1.</param>
        /// <returns>The a* and b* of the colour back minus the colour's own.</returns>
        static (double Da, double Db) RoundTrip(Configuration config, double r, double g, double b)
        {
            var colour = new Unicolour(config, ColourSpace.Rgb, r, g, b);
            var back = new Unicolour(config, colour.Icc);
            return (back.Lab.A - colour.Lab.A, back.Lab.B - colour.Lab.B);
        }

        /// <summary>
        /// The interpolated displacement of the pixel (r, g, b): the grid's (da, db) at its eight
        /// surrounding nodes, weighted trilinearly.
        /// </summary>
        /// <param name="t">A table Build made, without a Problem.</param>
        /// <param name="r">The encoded sRGB red, 0..1; clamped to that range.</param>
        /// <param name="g">The encoded sRGB green, 0..1; clamped to that range.</param>
        /// <param name="b">The encoded sRGB blue, 0..1; clamped to that range.</param>
        /// <returns>The displacement of a* and of b*.</returns>
        internal static (float Da, float Db) Interpolated(GamutTable t, float r, float g, float b)
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
            return (da, db);
        }

        /// <summary>
        /// The pixel (r, g, b) leaves the gamut: its displacement moves a*, b* by more than
        /// CurveGamutDeltaAb. The interpolated displacement decides, unless it lies within
        /// CurveGamutExactMargin of the threshold and the table has a profile behind it: then the
        /// exact round trip of the pixel's 8-bit colour decides, once per colour (ADR 0042).
        /// </summary>
        /// <param name="t">A table Build made, without a Problem.</param>
        /// <param name="r">The encoded sRGB red, 0..1; clamped to that range.</param>
        /// <param name="g">The encoded sRGB green, 0..1; clamped to that range.</param>
        /// <param name="b">The encoded sRGB blue, 0..1; clamped to that range.</param>
        /// <returns><c>true</c> when the colour is outside the paper's gamut.</returns>
        internal static bool Outside(GamutTable t, float r, float g, float b)
        {
            var (da, db) = Interpolated(t, r, g, b);
            double d = Math.Sqrt((double)da * da + (double)db * db);
            if (t.Exact != null && Math.Abs(d - Thresholds.CurveGamutDeltaAb) < Thresholds.CurveGamutExactMargin)
                return ExactOutside(t, r, g, b);
            return d > Thresholds.CurveGamutDeltaAb;
        }

        /// <summary>
        /// The exact answer for the pixel's 8-bit colour, from the table's cache or from one round
        /// trip that then joins it. The 8-bit quantisation is what makes the cache small: on the
        /// plate-solved FITS the 450 000 near pixels of the canvas profile are 29 000 colours.
        /// </summary>
        /// <param name="t">A table with an Exact trip.</param>
        /// <param name="r">The encoded sRGB red, 0..1.</param>
        /// <param name="g">The encoded sRGB green, 0..1.</param>
        /// <param name="b">The encoded sRGB blue, 0..1.</param>
        /// <returns><c>true</c> when the 8-bit colour's exact displacement passes the threshold.</returns>
        static bool ExactOutside(GamutTable t, float r, float g, float b)
        {
            int r8 = RasterOps.Level8(r), g8 = RasterOps.Level8(g), b8 = RasterOps.Level8(b);
            int key = (r8 << 16) | (g8 << 8) | b8;
            return t.Cache.GetOrAdd(key, _ =>
            {
                var (da, db) = t.Exact(r8 / 255.0, g8 / 255.0, b8 / 255.0);
                return da * da + db * db > Thresholds.CurveGamutDeltaAb * Thresholds.CurveGamutDeltaAb;
            });
        }

        /// <summary>
        /// The percentage of the pixels of the tonal subsample <paramref name="sub"/> outside the
        /// gamut, in all and over the five classes of the map, the rows measured in parallel; a
        /// mono image has no chroma and gives 0.
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
            var total = new Counts();
            if (sub.IsColor)
            {
                float[] R = sub.Planes[0], G = sub.Planes[1], B = sub.Planes[2];
                object gate = new();
                Parallel.For(0, sub.H, () => new Counts(), (y, _, acc) =>
                {
                    int by = y / map.Side;
                    for (int x = 0; x < sub.W; x++)
                    {
                        int i = y * sub.W + x;
                        bool outside = Outside(t, R[i], G[i], B[i]);
                        acc.All++; if (outside) acc.OutAll++;
                        int bx = x / map.Side;
                        if (bx >= map.Cols || by >= map.Rows) continue;
                        int c = (int)map.Classes[by * map.Cols + bx];
                        acc.N[c]++; if (outside) acc.O[c]++;
                    }
                    return acc;
                }, acc => { lock (gate) total.Add(acc); });
            }
            var per = new double[5];
            for (int c = 1; c <= 5; c++) per[c - 1] = total.N[c] == 0 ? (sub.IsColor ? double.NaN : 0) : 100.0 * total.O[c] / total.N[c];
            return new GamutFigure { PctAll = total.All == 0 ? 0 : 100.0 * total.OutAll / total.All, PctByClass = per };
        }

        /// <summary>The pixels counted by one worker over the rows it took, or by all: in all, outside, and per class.</summary>
        sealed class Counts
        {
            internal long All, OutAll;
            internal readonly long[] N = new long[6], O = new long[6];

            /// <summary>Adds another row's counts to these.</summary>
            /// <param name="other">The counts to add; left as they are.</param>
            internal void Add(Counts other)
            {
                All += other.All; OutAll += other.OutAll;
                for (int c = 0; c < 6; c++) { N[c] += other.N[c]; O[c] += other.O[c]; }
            }
        }
    }
}
