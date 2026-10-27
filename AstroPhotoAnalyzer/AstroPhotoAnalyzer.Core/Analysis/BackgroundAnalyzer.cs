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
// E1. The "background" is the sky: the pixels of the patches that R-044's classification calls
// sky (DustMap.Classes == Sky), without the bottom band of the image - the horizon sits there,
// with its light pollution, which is not "background". The R, G, B medians are taken on them
// (ADR 0055; before it, on the darkest quarter of the pixels, which on a frame the Great Rift
// crosses is the dust, brown, and made the whole sky read "bluer than the background"). A cast
// of 1-2 levels is invisible at a background of 0.10 and obvious at 0.19: that is why it is
// measured now, before the re-stretch.
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

        /// <summary>
        /// The region's medians, in levels 0..255, on each plane: of its sky pixels when
        /// <see cref="OnSkyPixels"/>, of every pixel otherwise.
        /// </summary>
        internal IReadOnlyList<double> Levels { get; init; }

        /// <summary>Region minus background, on each plane, in levels.</summary>
        internal IReadOnlyList<double> Deltas { get; init; }

        /// <summary>
        /// The share of the region's pixels that lie in sky patches (R-044), 0..1.
        /// </summary>
        internal double SkyFraction { get; init; }

        /// <summary>
        /// Were the medians taken on the sky pixels alone (the band, the dust and the nebulae
        /// left out)? False when <see cref="SkyFraction"/> is under
        /// <see cref="Thresholds.RegionSkyMinFraction"/>: then every pixel counts, so that a
        /// glow bright enough to be classed nebula cannot leave the region with the object.
        /// </summary>
        internal bool OnSkyPixels { get; init; }

        /// <summary>
        /// What the report and the window append to the region's row when it was measured on
        /// every pixel: the sky share and the fact; empty otherwise.
        /// </summary>
        internal string PixelsNote => OnSkyPixels
            ? ""
            : Tr.Pick($"  (sky {Fmt.F0(SkyFraction * 100)} %: every pixel)", $"  (cer {Fmt.F0(SkyFraction * 100)} %: toti pixelii)");

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

    /// <summary>Which pixels gave the sky's colour (E1), from the first choice down.</summary>
    internal enum SkyColourSource
    {
        /// <summary>The pixels of the sky patches above the bottom band: the normal case.</summary>
        SkyPatches,

        /// <summary>
        /// The pixels of every patch above the bottom band: no sky patch lies there (the frame
        /// is all band, nebula or dust).
        /// </summary>
        AllPatches,

        /// <summary>
        /// Every pixel: the frame holds no patch. A guard only: the map's grid keeps at least
        /// one patch, its side shrunk to the frame (DustAnalyzer.Grid), so no image reaches it.
        /// </summary>
        WholeFrame,
    }

    /// <summary>The sky's colour (E1), and the edge regions measured against it (E2).</summary>
    internal sealed record BackgroundResult
    {
        /// <summary>
        /// The background's medians, in levels, on each plane (a single one on mono).
        /// </summary>
        internal IReadOnlyList<double> Levels { get; init; }

        /// <summary>Which pixels <see cref="Levels"/> was taken on.</summary>
        internal SkyColourSource Source { get; init; }

        /// <summary>
        /// <see cref="Source"/> in words, as the report's Appendix E and the window print it.
        /// </summary>
        internal string SourceText => Source switch
        {
            SkyColourSource.SkyPatches => Tr.Pick("the sky patches above the bottom band", "dalele de cer de deasupra benzii de jos"),
            SkyColourSource.AllPatches => Tr.Pick("every patch above the bottom band (no sky patch there)", "toate dalele de deasupra benzii de jos (nicio dala de cer acolo)"),
            _ => Tr.Pick("the whole frame (too small for a patch)", "tot cadrul (prea mic pentru o dala)"),
        };

        /// <summary>The cast metrics make sense only on colour.</summary>
        internal bool ColorApplicable { get; init; }

        /// <summary>
        /// G - (R + B) / 2, in levels: positive = green, negative = magenta. A figure that names
        /// the cast's direction; the verdict is <see cref="Chroma"/>'s (ADR 0057).
        /// </summary>
        internal double GreenExcess { get; init; }

        /// <summary>R - B, in levels: positive = warm, negative = cool. A figure, as above.</summary>
        internal double RedMinusBlue { get; init; }

        /// <summary>
        /// The sky's colour in CIE L*a*b*: L* (0..100), a* (negative green, positive magenta)
        /// and b* (negative blue, positive yellow). 0 on mono.
        /// </summary>
        internal (double L, double A, double B) Lab { get; init; }

        /// <summary>
        /// The distance of the sky's colour from a grey of the same lightness,
        /// sqrt(a*^2 + b*^2): the size of the cast in the unit the eye works in, which a gamma
        /// lift of the background barely changes. 0 on mono.
        /// </summary>
        internal double Chroma => Math.Sqrt(Lab.A * Lab.A + Lab.B * Lab.B);

        /// <summary>A cast: the chroma over <see cref="Thresholds.CastWarnChroma"/>.</summary>
        internal bool HasCast => ColorApplicable && Chroma > Thresholds.CastWarnChroma;

        internal IReadOnlyList<EdgeRegion> Regions { get; init; }
        internal IEnumerable<EdgeRegion> FlaggedRegions => Regions.Where(x => x.Flagged);
    }

    /// <summary>Measures the sky's colour and the edge regions against it (E1, E2).</summary>
    internal static class BackgroundAnalyzer
    {
        /// <summary>
        /// <see cref="Analyze(Raster, DustMap)"/> with a map built here from the image alone,
        /// without the catalogue: for the tests, which have no simulation to take it from.
        /// </summary>
        /// <param name="full">The image, 0..1.</param>
        /// <returns>The background's levels, the cast and the regions.</returns>
        internal static BackgroundResult Analyze(Raster full)
            => Analyze(full, DustAnalyzer.Build(full.W, full.H, full.Source?.Wcs,
                DustAnalyzer.PatchLstars(RasterOps.Subsample(full, Thresholds.TonalStride))));

        /// <summary>
        /// The sky's colour: the medians, on each plane, of the pixels of the sky patches of
        /// <paramref name="map"/> that lie above the bottom
        /// <see cref="Thresholds.BackgroundExcludeBottomFraction"/> of the rows (of every patch
        /// there when none is sky, of every pixel when the frame holds no patch); then the eight
        /// edge regions, on the same subsample and each on its own sky pixels, against it.
        /// </summary>
        /// <param name="full">The image, 0..1.</param>
        /// <param name="map">
        /// The classes of R-044 on the image as it is (the simulation's map), whose grid is read
        /// on the subsample at its own stride.
        /// </param>
        /// <returns>
        /// The background's levels, which pixels gave them, the cast and the regions.
        /// </returns>
        internal static BackgroundResult Analyze(Raster full, DustMap map)
        {
            // The map's own subsample, so that its grid can be read on it: the stride-3
            // subsample of the acceptance script went with ADR 0056.
            Raster s = RasterOps.Subsample(full, map.Stride);
            int planes = s.Planes.Length;
            (double[] bg, SkyColourSource source) = SkyColour(s, map);

            bool color = planes == 3;
            return new BackgroundResult
            {
                Levels = bg,
                Source = source,
                ColorApplicable = color,
                GreenExcess = color ? bg[1] - (bg[0] + bg[2]) / 2 : 0,
                RedMinusBlue = color ? bg[0] - bg[2] : 0,
                Lab = color ? ColorMath.LabOfSrgb(bg[0] / 255, bg[1] / 255, bg[2] / 255) : (0, 0, 0),
                Regions = Regions(s, map, bg),
            };
        }

        /// <summary>
        /// The class of the patch a sample of the map's subsample lies in; None past the last
        /// whole patch (the grid drops the partial patches at the right and bottom edges).
        /// </summary>
        /// <param name="map">The classes of R-044 on the image as it is.</param>
        /// <param name="x">The sample's column on the map's subsample.</param>
        /// <param name="y">The sample's row on the map's subsample.</param>
        /// <returns>The patch's class, or None when the sample belongs to no patch.</returns>
        static PatchClass ClassAt(DustMap map, int x, int y)
        {
            int bx = x / map.Side, by = y / map.Side;
            return bx < map.Cols && by < map.Rows ? map.Classes[by * map.Cols + bx] : PatchClass.None;
        }

        /// <summary>
        /// The sky's colour (E1) on the subsample the map's grid was built on: the pixels of the
        /// sky patches above the bottom band, then, when there is none, of every patch there,
        /// then of the whole frame.
        /// </summary>
        /// <param name="sub">The image's subsample at the map's stride, 0..1.</param>
        /// <param name="map">The classes of R-044 on the image as it is.</param>
        /// <returns>The medians x 255 on each plane, and which pixels gave them.</returns>
        static (double[] Levels, SkyColourSource Source) SkyColour(Raster sub, DustMap map)
        {
            int planes = sub.Planes.Length;
            int rowLimit = (int)(sub.H * (1 - Thresholds.BackgroundExcludeBottomFraction));

            // A sample past the last whole patch belongs to no patch and to no tier but the
            // whole frame.
            var sky = new List<int>();
            var grid = new List<int>();
            for (int y = 0; y < rowLimit; y++)
                for (int x = 0; x < sub.W; x++)
                {
                    PatchClass c = ClassAt(map, x, y);
                    if (c == PatchClass.None) continue;
                    int i = y * sub.W + x;
                    grid.Add(i);
                    if (c == PatchClass.Sky) sky.Add(i);
                }

            List<int> picked;
            SkyColourSource source;
            if (sky.Count > 0) (picked, source) = (sky, SkyColourSource.SkyPatches);
            else if (grid.Count > 0) (picked, source) = (grid, SkyColourSource.AllPatches);
            else (picked, source) = (Enumerable.Range(0, sub.W * sub.H).ToList(), SkyColourSource.WholeFrame);

            var bg = new double[planes];
            for (int p = 0; p < planes; p++)
            {
                var vals = new float[picked.Count];
                for (int k = 0; k < picked.Count; k++) vals[k] = sub.Planes[p][picked[k]];
                bg[p] = Stats.Median(vals) * 255;
            }
            return (bg, source);
        }

        /// <summary>
        /// The eight edge regions of the subsample, each with its medians, on its sky pixels when
        /// they are at least <see cref="Thresholds.RegionSkyMinFraction"/> of it and on every
        /// pixel otherwise (ADR 0056), and its departure from the background; a region with no
        /// width or no height is left out.
        /// </summary>
        /// <param name="s">The image's subsample at the map's stride, 0..1.</param>
        /// <param name="map">The classes of R-044 on the image as it is.</param>
        /// <param name="bg">The background's level on each plane, 0..255.</param>
        /// <returns>
        /// The regions: the four corners (top-left, top-right, bottom-left, bottom-right), then
        /// the four sides (top, bottom, left, right).
        /// </returns>
        static IReadOnlyList<EdgeRegion> Regions(Raster s, DustMap map, double[] bg)
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

                // The region's sky pixels: the object entering a corner (the band, a dust lane)
                // must not pass for a gradient of the sky. Under the floor, every pixel: a glow
                // bright enough to be classed nebula would otherwise leave with the object.
                var sky = new List<int>();
                for (int y = d.Y0; y < d.Y0 + d.H; y++)
                    for (int x = d.X0; x < d.X0 + d.W; x++)
                        if (ClassAt(map, x, y) == PatchClass.Sky) sky.Add(y * s.W + x);
                double skyFraction = sky.Count / (double)(d.W * d.H);
                bool onSky = skyFraction >= Thresholds.RegionSkyMinFraction;

                var levels = new double[s.Planes.Length];
                var deltas = new double[s.Planes.Length];
                var buf = new float[onSky ? sky.Count : d.W * d.H];
                for (int p = 0; p < s.Planes.Length; p++)
                {
                    int k = 0;
                    if (onSky)
                        foreach (int i in sky) buf[k++] = s.Planes[p][i];
                    else
                        for (int y = d.Y0; y < d.Y0 + d.H; y++)
                            for (int x = d.X0; x < d.X0 + d.W; x++)
                                buf[k++] = s.Planes[p][y * s.W + x];
                    levels[p] = Stats.Median(buf) * 255;
                    deltas[p] = levels[p] - bg[p];
                }
                list.Add(new EdgeRegion
                {
                    Name = d.Name,
                    X0 = d.X0,
                    Y0 = d.Y0,
                    W = d.W,
                    H = d.H,
                    Levels = levels,
                    Deltas = deltas,
                    SkyFraction = skyFraction,
                    OnSkyPixels = onSky,
                });
            }
            return list;
        }
    }
}
