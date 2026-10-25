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

// BackgroundAnalyzer.cs - the measurements E1 and E2: what colour the sky is, and whether any
// corner or side departs from it.
//
// E1. The "background" is the darkest quarter of the pixels (by the channels' mean luminance),
// without the bottom band of the image - the horizon sits there, with its light pollution, which
// is not "background". The R, G, B medians are taken on it. A cast of 1-2 levels is invisible at
// a background of 0.10 and obvious at 0.19: that is why it is measured now, before the
// re-stretch.
//
// E2. Eight regions on the edge (4 corners, 4 sides), each with its own R, G, B medians against
// the background. It compares not the luminance but EACH channel: the horizon corner of the
// acceptance image had the same luminance as the sky (R +9, B -10), and a comparison on
// luminance would not have seen it.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A region of the frame's edge (E2): its rectangle in samples of the subsample, its levels and
    /// its departure from the background.
    /// </summary>
    internal sealed record EdgeRegion
    {
        /// <summary>
        /// "colt stanga-sus", "latura dreapta" and so on - the names in the report.
        /// </summary>
        internal string Name { get; init; }
        internal int X0 { get; init; }
        internal int Y0 { get; init; }
        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>The region's medians, in levels 0..255, on each plane.</summary>
        internal IReadOnlyList<double> Levels { get; init; }

        /// <summary>Region minus background, on each plane, in levels.</summary>
        internal IReadOnlyList<double> Deltas { get; init; }

        internal double MaxAbsDelta => Deltas.Max(d => Math.Abs(d));
        internal bool Flagged => MaxAbsDelta > Thresholds.CornerGradientLevels;

        /// <summary>
        /// Does the departure have COLOUR (the channels move differently), not just brightness? On
        /// mono the notion does not exist. See Thresholds.CornerColourSpreadRatio.
        /// </summary>
        internal bool Coloured
        {
            get
            {
                if (Deltas.Count < 3) return false;
                double spread = Deltas.Max() - Deltas.Min();
                double mean = Deltas.Average(d => Math.Abs(d));
                return spread > Thresholds.CornerGradientLevels && spread > Thresholds.CornerColourSpreadRatio * mean;
            }
        }
    }

    /// <summary>The sky's colour (E1), and the edge regions measured against it (E2).</summary>
    internal sealed record BackgroundResult
    {
        /// <summary>
        /// The background's medians, in levels, on each plane (a single one on mono).
        /// </summary>
        internal IReadOnlyList<double> Levels { get; init; }

        /// <summary>The cast metrics make sense only on colour.</summary>
        internal bool ColorApplicable { get; init; }

        /// <summary>G - (R + B) / 2: positive = green, negative = magenta.</summary>
        internal double GreenExcess { get; init; }

        /// <summary>R - B: positive = warm, negative = cool.</summary>
        internal double RedMinusBlue { get; init; }

        internal bool HasCast => ColorApplicable
            && (Math.Abs(GreenExcess) > Thresholds.CastWarnLevels || Math.Abs(RedMinusBlue) > Thresholds.CastWarnLevels);

        internal IReadOnlyList<EdgeRegion> Regions { get; init; }
        internal IEnumerable<EdgeRegion> FlaggedRegions => Regions.Where(x => x.Flagged);
    }

    /// <summary>Measures the sky's colour and the edge regions against it (E1, E2).</summary>
    internal static class BackgroundAnalyzer
    {
        /// <summary>
        /// The background's colour on the subsample at <see cref="Thresholds.BackgroundStride"/>:
        /// the medians of the pixels at or under the
        /// <see cref="Thresholds.BackgroundDarkestFraction"/> percentile of the luminance (the
        /// mean of the planes), the bottom <see cref="Thresholds.BackgroundExcludeBottomFraction"/>
        /// of the rows left out; then the eight edge regions against it.
        /// </summary>
        /// <param name="full">The image, 0..1.</param>
        /// <returns>
        /// The background's levels (0 on a plane when no pixel was picked), the cast and the
        /// regions.
        /// </returns>
        internal static BackgroundResult Analyze(Raster full)
        {
            Raster s = RasterOps.Subsample(full, Thresholds.BackgroundStride);
            int n = s.H * s.W, planes = s.Planes.Length;

            // A simple luminance, the mean of the planes - as in the script. The threshold is
            // taken over the WHOLE subsample, then the bottom band is removed: that is how the
            // script was, and that way the background stays "the darkest quarter of the image",
            // not of its upper part.
            var lum = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = 0;
                for (int p = 0; p < planes; p++) t += s.Planes[p][i];
                lum[i] = t / planes;
            }
            double cut = Stats.Percentile(lum, 100 * Thresholds.BackgroundDarkestFraction);
            int rowLimit = (int)(s.H * (1 - Thresholds.BackgroundExcludeBottomFraction));

            // "<=", not "<": on a flat sky many pixels sit EXACTLY at the percentile, and a strict
            // "<" would pick none. Real data ties too: on the 16-bit reference image 68 pixels
            // above the bottom band sit at the cut and the printed levels do not move; on its
            // 8-bit version 32442 do, and the two give a green cast of +2.0 and +1.0 levels, either
            // side of CastWarnLevels (measured 2026-10-23).
            var picked = new List<int>();
            for (int y = 0; y < rowLimit; y++)
                for (int x = 0; x < s.W; x++)
                    if (lum[y * s.W + x] <= cut) picked.Add(y * s.W + x);

            var bg = new double[planes];
            for (int p = 0; p < planes; p++)
            {
                var vals = new float[picked.Count];
                for (int k = 0; k < picked.Count; k++) vals[k] = s.Planes[p][picked[k]];
                bg[p] = vals.Length == 0 ? 0 : Stats.Median(vals) * 255;
            }

            bool color = planes == 3;
            return new BackgroundResult
            {
                Levels = bg,
                ColorApplicable = color,
                GreenExcess = color ? bg[1] - (bg[0] + bg[2]) / 2 : 0,
                RedMinusBlue = color ? bg[0] - bg[2] : 0,
                Regions = Regions(s, bg),
            };
        }

        /// <summary>
        /// The eight edge regions of the subsample, each with its medians and its departure from
        /// the background; a region with no width or no height is left out.
        /// </summary>
        /// <param name="s">The subsample, 0..1.</param>
        /// <param name="bg">The background's level on each plane, 0..255.</param>
        /// <returns>
        /// The regions: the four corners (top-left, top-right, bottom-left, bottom-right), then
        /// the four sides (top, bottom, left, right).
        /// </returns>
        static IReadOnlyList<EdgeRegion> Regions(Raster s, double[] bg)
        {
            // The corner: 15 % of the height x 20 % of the width (the script's region). The side:
            // a band of the same thickness, along the middle of the side, between the corners.
            int ch = (int)(s.H * Thresholds.EdgeRegionHeightFraction), cw = (int)(s.W * Thresholds.EdgeRegionWidthFraction);
            var defs = new (string Name, int X0, int Y0, int W, int H)[]
            {
                (Tr.Pick("upper-left corner", "colt stanga-sus"), 0, 0,         cw,           ch),
                (Tr.Pick("upper-right corner", "colt dreapta-sus"), s.W - cw, 0,         cw,           ch),
                (Tr.Pick("lower-left corner", "colt stanga-jos"), 0, s.H - ch,  cw,           ch),
                (Tr.Pick("lower-right corner", "colt dreapta-jos"), s.W - cw, s.H - ch,  cw,           ch),
                (Tr.Pick("top edge", "latura de sus"), cw, 0,         s.W - 2 * cw, ch),
                (Tr.Pick("bottom edge", "latura de jos"), cw, s.H - ch,  s.W - 2 * cw, ch),
                (Tr.Pick("left edge", "latura stanga"), 0, ch,        cw,           s.H - 2 * ch),
                (Tr.Pick("right edge", "latura dreapta"), s.W - cw, ch,        cw,           s.H - 2 * ch),
            };

            var list = new List<EdgeRegion>();
            foreach (var d in defs)
            {
                if (d.W <= 0 || d.H <= 0) continue;
                var levels = new double[s.Planes.Length];
                var deltas = new double[s.Planes.Length];
                var buf = new float[d.W * d.H];
                for (int p = 0; p < s.Planes.Length; p++)
                {
                    int k = 0;
                    for (int y = d.Y0; y < d.Y0 + d.H; y++)
                        for (int x = d.X0; x < d.X0 + d.W; x++)
                            buf[k++] = s.Planes[p][y * s.W + x];
                    levels[p] = Stats.Median(buf) * 255;
                    deltas[p] = levels[p] - bg[p];
                }
                list.Add(new EdgeRegion { Name = d.Name, X0 = d.X0, Y0 = d.Y0, W = d.W, H = d.H, Levels = levels, Deltas = deltas });
            }
            return list;
        }
    }
}
