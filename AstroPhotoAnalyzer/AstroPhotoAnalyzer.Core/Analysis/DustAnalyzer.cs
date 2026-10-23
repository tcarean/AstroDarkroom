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

// DustAnalyzer.cs - the measurement B5: where the dust sits, the darkest EXTENDED part of the
// image, and where the sky sits, in L*.
//
// Not on pixels: the darkest pixels are noise and isolated specks. On PATCHES of
// Thresholds.DustPatchPx x DustPatchPx pixels of the image, each with the L* of its mean LINEAR
// luminance - what the eye integrates over a 4 mm spot of paper. The patches are split into
// CLASSES, relatively, not by an absolute threshold, on one of two paths:
//   - with a WCS and with the Milky Way's band in the frame (ADR 0026): the band is geometry,
//     from the galactic latitude; the sky is a smooth model from the patches OUTSIDE the band;
//     the band's local level is a median over a window; the dust is a patch DustDepthLstar under
//     the local level (or under the modelled sky), the bright clouds BrightCloudLstar over the
//     local level, the peaks the brightest PeakFraction of the patches, the band also every
//     patch outside it BrightCloudLstar over the modelled sky, the sky the rest outside the band;
//   - otherwise (no WCS, a frame without the band or wholly inside it, or a frame spanning under
//     BandMinLatitudeSpanDeg degrees of galactic latitude, deep-sky in the band; ADR 0033,
//     ADR 0037): the sky is a smooth model from ALL the patches, with the SkyModelLowPercentile
//     percentile per cell; the thresholds come from the image: the nebula is a patch more than
//     SkyNoiseK sky sigmas over the model, the dust as much under it, the bright nebula over the
//     Otsu threshold of the nebula's tail (without the peaks), the peaks as above, the sky the
//     rest.
// On either path the verdict measures the same way (Measure): the sky is the median of the sky
// class; the dust is the darkest extended area, decided ONCE on the image as it is
// (DecideDustSource): the DustClassFraction percentile of the dust class when the class holds
// DustClassMinFraction of the patches (under that it is cores or artefacts) and is at or under
// the SkyDarkestFraction percentile of the sky class, otherwise the latter; at every lift the
// same statistic is re-measured, so that the ladder's rows name the same dust as the definition.
// The Lynds catalogue (DarkNebulaCatalogue) gives the clouds' NAMES and each one's row. The
// geometry (DustMap) is made once; the L*s are redone at every lift.
//
// It works on the tonal subsample (step 4): a patch is 8 x 8 samples. The partial patches at the
// right and bottom edges are left out.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>Which path made the classes.</summary>
    internal enum DustMethod
    {
        /// <summary>
        /// A WCS, the band and sky outside it, in a frame spanning at least
        /// BandMinLatitudeSpanDeg of galactic latitude: the band classes (ADR 0026, ADR 0037).
        /// </summary>
        Band,

        /// <summary>
        /// The classes relative to the modelled sky (ADR 0033); DustMap.SkyReason says why not the
        /// band.
        /// </summary>
        Sky,
    }

    /// <summary>Why the band classes were not made.</summary>
    internal enum SkyReason
    {
        /// <summary>The classes are the band's.</summary>
        None,

        /// <summary>The file has no WCS.</summary>
        NoWcs,

        /// <summary>
        /// A WCS, but the galactic band is under BandMinFraction of the frame (deep-sky far from
        /// the plane).
        /// </summary>
        NoBand,

        /// <summary>
        /// A WCS, but the sky outside the band is under BandMinFraction of the frame (deep-sky in
        /// the plane).
        /// </summary>
        AllBand,

        /// <summary>
        /// A WCS, band and sky on both sides of |b| = 10, but the patches span under
        /// BandMinLatitudeSpanDeg degrees of galactic latitude: a deep-sky frame in the band, not
        /// a landscape with it (ADR 0037).
        /// </summary>
        NarrowField,
    }

    /// <summary>
    /// A catalogued cloud with at least one patch in the frame: which patches are its own, and
    /// where it is on the image.
    /// </summary>
    internal sealed class DustCloud
    {
        internal DarkNebula Nebula;

        /// <summary>
        /// The indices of the patches (in the map's row order) whose centre lies in the disc,
        /// among those inside the disc's bounding box in pixels: far from the tangent point the
        /// projection stretches a large disc past that box, and its outer patches are not here.
        /// </summary>
        internal int[] Patches;

        /// <summary>
        /// The disc's centre, in pixels of the image (row 0 at the top), and its radius in pixels.
        /// </summary>
        internal double CentreX, CentreY, RadiusPx;

        internal string Name => Nebula.Name;
    }

    /// <summary>
    /// The class of a patch. Band and BrightCloud are "banda" and "nori luminosi" on the band
    /// path, "nebuloasa" and "nebuloasa stralucitoare" on the sky path (DustAnalyzer.ClassNames);
    /// Build never gives None.
    /// </summary>
    internal enum PatchClass : byte
    {
        None = 0,
        Dust = 1,
        Sky = 2,
        Band = 3,
        BrightCloud = 4,
        Peak = 5,
    }

    /// <summary>
    /// The measurement's geometry: the grid of patches, the class of each and the catalogued
    /// clouds. It is built once per image, on its L*s as it is, and reused at every lift: the
    /// classes do not move with the lift, only the values.
    /// </summary>
    internal sealed class DustMap
    {
        internal int Cols, Rows;

        /// <summary>
        /// The side of a patch in samples of the subsample, and the subsample's step.
        /// </summary>
        internal int Side, Stride;
        internal DustMethod Method;
        internal SkyReason SkyReason;

        /// <summary>The class of each patch.</summary>
        internal PatchClass[] Classes;

        /// <summary>
        /// The galactic latitude of each patch's centre, in degrees; null without a WCS.
        /// </summary>
        internal float[] GalacticLatitude;

        /// <summary>
        /// How many degrees of galactic latitude the patch centres span (the maximum minus the
        /// minimum); NaN without a WCS.
        /// </summary>
        internal double LatitudeSpanDeg = double.NaN;

        /// <summary>The modelled sky, an L* per patch, on the image as it is.</summary>
        internal float[] SkyModel;

        /// <summary>
        /// The band's local level, an L* per patch, on the image as it is; null on the sky path.
        /// </summary>
        internal float[] BandLevel;

        /// <summary>
        /// On the sky path: the sky's noise, the robust sigma of every patch's residual from the
        /// modelled sky after one clipping pass; NoiseThreshold = SkyNoiseK x sigma, the distance
        /// in L* from the modelled sky from which a patch is nebula (upwards) or dust (downwards);
        /// BrightThreshold, in L* over the modelled sky, from which it is bright nebula (infinity
        /// when the nebula's tail holds fewer than two patches, and then there is none). NaN on
        /// the band path.
        /// </summary>
        internal double SkySigma = double.NaN, NoiseThreshold = double.NaN, BrightThreshold = double.NaN;

        /// <summary>
        /// What gives the dust's L*, decided once on the image as it is
        /// (DustAnalyzer.DecideDustSource).
        /// </summary>
        internal DustSource DustSource;

        /// <summary>
        /// The clouds with at least one patch in the frame, in the catalogue's order; empty
        /// without a WCS.
        /// </summary>
        internal IReadOnlyList<DustCloud> Clouds;

        internal int PatchCount => Cols * Rows;

        /// <summary>The fraction of the patches in each class, by PatchClass.</summary>
        /// <param name="c">The class; None gives 0, as Build gives it to no patch.</param>
        /// <returns>The fraction of the patches in the class, 0..1.</returns>
        internal double FractionOf(PatchClass c) => Classes.Count(x => x == c) / (double)PatchCount;

        /// <summary>
        /// No sky patch (all are dust, nebula or peaks): the verdict's sky is the median of all
        /// the patches.
        /// </summary>
        internal bool SkyClassEmpty => Array.IndexOf(Classes, PatchClass.Sky) < 0;

        /// <summary>
        /// The names of the five classes on the map's path, in the order "praf", "cer", "banda" /
        /// "nebuloasa", "nori luminosi" / "nebuloasa stralucitoare", "varfuri".
        /// </summary>
        internal IReadOnlyList<string> ClassNames => DustAnalyzer.ClassNames(Method);

        /// <summary>The centre of the patch (bx, by), in pixels of the whole image.</summary>
        /// <param name="bx">The patch's column on the grid, from 0.</param>
        /// <param name="by">The patch's row on the grid, from 0 at the top.</param>
        /// <returns>
        /// The column and the row of the centre, in pixels of the whole image, row 0 at the top
        /// and the centre of pixel 0 at 0.
        /// </returns>
        internal (double X, double Y) PatchCentre(int bx, int by)
            => ((bx + 0.5) * Side * Stride - 0.5, (by + 0.5) * Side * Stride - 0.5);
    }

    /// <summary>What gives the dust's L*: the dust class, or the darkest sky.</summary>
    internal enum DustSource
    {
        /// <summary>
        /// The DustClassFraction percentile of the dust class, which holds at least
        /// DustClassMinFraction of the patches and is, as it is, at or under the darkest sky.
        /// </summary>
        DustClass,

        /// <summary>
        /// The SkyDarkestFraction percentile of the sky class (of all the patches when the sky
        /// class is empty): the dust class is empty, too small, or lighter.
        /// </summary>
        DarkestSky,
    }

    /// <summary>The dust and the sky on a map, at a given lift.</summary>
    internal sealed record DustStats
    {
        internal double LstarDust { get; init; }
        internal double LstarSky { get; init; }

        /// <summary>
        /// The percentage of the frame's patches with an L* under paper black - "negru pur".
        /// </summary>
        internal double PctPatchesBelowPaperBlack { get; init; }

        /// <summary>The L* of each patch, in the map's row order.</summary>
        internal float[] PatchLstars { get; init; }

        /// <summary>
        /// The DustCloudFraction percentile of each cloud's patches in DustMap.Clouds, in the same
        /// order.
        /// </summary>
        internal double[] CloudLstars { get; init; }

        /// <summary>How many patches the dust class holds.</summary>
        internal int DustPatchCount { get; init; }
    }

    /// <summary>
    /// The measurement B5: the patches' L*s, the map of their classes on the band or the sky
    /// path, and the dust and the sky measured on that map.
    /// </summary>
    internal static class DustAnalyzer
    {
        /// <summary>The side of a patch in samples of the tonal subsample.</summary>
        internal const int PatchSamples = Thresholds.DustPatchPx / Thresholds.TonalStride;

        static readonly string[] BandNames = { "praf", "cer", "banda", "nori luminosi", "varfuri" };
        static readonly string[] SkyNames = { "praf", "cer", "nebuloasa", "nebuloasa stralucitoare", "varfuri" };

        /// <summary>
        /// The names of the five classes, in the order "praf", "cer", "banda" / "nebuloasa",
        /// "nori luminosi" / "nebuloasa stralucitoare", "varfuri": what the map, the curves' table
        /// and the report show.
        /// </summary>
        /// <param name="method">
        /// The path: Band names the third and fourth class "banda" and "nori luminosi", Sky
        /// "nebuloasa" and "nebuloasa stralucitoare".
        /// </param>
        /// <returns>The five names, in the order of PatchClass from Dust to Peak.</returns>
        internal static IReadOnlyList<string> ClassNames(DustMethod method) => method == DustMethod.Band ? BandNames : SkyNames;

        /// <summary>
        /// The patch map of an image of <paramref name="w"/> x <paramref name="h"/> pixels,
        /// subsampled with <paramref name="stride"/>, on its L*s as it is
        /// (<paramref name="asIsPatchLstars"/>, from PatchLstars on the same grid): with a WCS,
        /// the galactic latitude of each patch and the catalogued clouds that claim it by its
        /// distance to their centre; then the classes, the band's when they can be made,
        /// otherwise relative to the modelled sky; then the dust's source (DecideDustSource).
        /// </summary>
        /// <param name="w">The image's width, in pixels.</param>
        /// <param name="h">The image's height, in pixels.</param>
        /// <param name="wcs">
        /// The image's WCS; null takes the sky path, with <see cref="SkyReason.NoWcs"/>.
        /// </param>
        /// <param name="asIsPatchLstars">
        /// The L* of each patch of the image as it is, in the map's row order: exactly one per
        /// patch of the grid of <paramref name="w"/>, <paramref name="h"/> and
        /// <paramref name="stride"/>, as <see cref="PatchLstars(Raster)"/> gives them on the
        /// subsample.
        /// </param>
        /// <param name="catalogue">
        /// The dark nebulae to look for; null takes the whole Lynds catalogue
        /// (<see cref="DarkNebulaCatalogue.All"/>). Read only with a WCS.
        /// </param>
        /// <param name="stride">
        /// The step of the subsample the patch L*s were measured on; by default the tonal one.
        /// </param>
        /// <returns>
        /// The map: its grid, classes, models, thresholds, clouds and dust source.
        /// </returns>
        internal static DustMap Build(int w, int h, Wcs wcs, float[] asIsPatchLstars, IReadOnlyList<DarkNebula> catalogue = null, int stride = Thresholds.TonalStride)
        {
            var (cols, rows, side) = Grid(w, h, stride);
            var map = new DustMap
            {
                Cols = cols,
                Rows = rows,
                Side = side,
                Stride = stride,
                Classes = new PatchClass[cols * rows],
                Clouds = Array.Empty<DustCloud>(),
            };
            if (asIsPatchLstars.Length != cols * rows) throw new ArgumentException("L*-urile dalelor nu sunt pe grila hartii");
            if (wcs == null)
            {
                ClassifyBySky(map, asIsPatchLstars, SkyReason.NoWcs);
                map.DustSource = DecideDustSource(asIsPatchLstars, map);
                return map;
            }

            catalogue ??= DarkNebulaCatalogue.All;

            // The sky direction and the galactic latitude of each patch's centre, once.
            int n = cols * rows;
            var vx = new double[n]; var vy = new double[n]; var vz = new double[n];
            map.GalacticLatitude = new float[n];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    var (cx, cy) = map.PatchCentre(bx, by);
                    var (ra, dec) = wcs.PixelToSky(cx, cy);
                    var v = Astrometry.UnitVector(ra, dec);
                    int i = by * cols + bx;
                    vx[i] = v.X; vy[i] = v.Y; vz[i] = v.Z;
                    map.GalacticLatitude[i] = (float)Astrometry.GalacticLatitude(ra, dec);
                }
            map.LatitudeSpanDeg = map.GalacticLatitude.Max() - map.GalacticLatitude.Min();

            map.Clouds = FindClouds(map, wcs, catalogue, w, h, vx, vy, vz);
            Classify(map, asIsPatchLstars);
            map.DustSource = DecideDustSource(asIsPatchLstars, map);
            return map;
        }

        /// <summary>
        /// The patch grid for a whole image: its subsample, then SubsampleGrid.
        /// </summary>
        /// <param name="w">The image's width, in pixels.</param>
        /// <param name="h">The image's height, in pixels.</param>
        /// <param name="stride">The subsampling step.</param>
        /// <returns>The grid's columns and rows, and a patch's side in samples.</returns>
        static (int Cols, int Rows, int Side) Grid(int w, int h, int stride)
            => SubsampleGrid((w + stride - 1) / stride, (h + stride - 1) / stride);

        /// <summary>
        /// The patch grid on a subsample of subW x subH: whole patches of PatchSamples; on a
        /// subsample with a side under PatchSamples, patches as wide as that side.
        /// </summary>
        /// <param name="subW">The subsample's width, in samples.</param>
        /// <param name="subH">The subsample's height, in samples.</param>
        /// <returns>
        /// The grid's columns and rows, and a patch's side in samples; the samples past the last
        /// whole patch, at the right and at the bottom, belong to none.
        /// </returns>
        static (int Cols, int Rows, int Side) SubsampleGrid(int subW, int subH)
        {
            int side = Math.Max(1, Math.Min(PatchSamples, Math.Min(subW, subH)));
            return (subW / side, subH / side, side);
        }

        /// <summary>
        /// The catalogued clouds with at least one patch centre in their disc, among the patches
        /// inside the disc's bounding box in pixels. The disc is the catalogue's area around the
        /// catalogue's centre; a cloud whose centre does not project (behind the tangent plane)
        /// or whose box misses the frame is skipped; a patch is tested by its angular distance to
        /// the centre. The box is the radius at the central plate scale, so far from the tangent
        /// point, where the projection stretches the disc, a cloud can lose its outer patches, or
        /// be skipped though its disc holds patch centres.
        /// </summary>
        /// <param name="map">The map, for its grid.</param>
        /// <param name="wcs">The image's WCS.</param>
        /// <param name="catalogue">The clouds to look for.</param>
        /// <param name="w">The image's width, in pixels.</param>
        /// <param name="h">The image's height, in pixels.</param>
        /// <param name="vx">
        /// The x of the unit vector toward each patch's centre on the sky, in the map's row
        /// order.
        /// </param>
        /// <param name="vy">The y of the same vectors.</param>
        /// <param name="vz">The z of the same vectors.</param>
        /// <returns>The clouds with at least one patch, in the catalogue's order.</returns>
        static List<DustCloud> FindClouds(DustMap map, Wcs wcs, IReadOnlyList<DarkNebula> catalogue, int w, int h, double[] vx, double[] vy, double[] vz)
        {
            int cols = map.Cols, rows = map.Rows;
            double patchPx = map.Side * map.Stride;
            var clouds = new List<DustCloud>();
            foreach (DarkNebula nebula in catalogue)
            {
                var (px, py) = wcs.SkyToPixel(nebula.Ra, nebula.Dec);
                if (double.IsNaN(px)) continue;
                double rpx = nebula.RadiusDeg / wcs.ScaleDegPerPx;
                // The disc does not touch the frame: out. Otherwise only the patches in its
                // rectangle are tested, on the true angular distance.
                if (px + rpx < 0 || py + rpx < 0 || px - rpx > w || py - rpx > h) continue;
                int bx0 = Math.Max(0, (int)Math.Floor((px - rpx) / patchPx)), bx1 = Math.Min(cols - 1, (int)Math.Ceiling((px + rpx) / patchPx));
                int by0 = Math.Max(0, (int)Math.Floor((py - rpx) / patchPx)), by1 = Math.Min(rows - 1, (int)Math.Ceiling((py + rpx) / patchPx));
                if (bx0 > bx1 || by0 > by1) continue;
                var c = Astrometry.UnitVector(nebula.Ra, nebula.Dec);
                double cosR = Math.Cos(nebula.RadiusDeg * Astrometry.Deg);
                var patches = new List<int>();
                for (int by = by0; by <= by1; by++)
                    for (int bx = bx0; bx <= bx1; bx++)
                    {
                        int i = by * cols + bx;
                        if (vx[i] * c.X + vy[i] * c.Y + vz[i] * c.Z >= cosR) patches.Add(i);
                    }
                if (patches.Count == 0) continue;
                clouds.Add(new DustCloud { Nebula = nebula, Patches = patches.ToArray(), CentreX = px, CentreY = py, RadiusPx = rpx });
            }
            return clouds;
        }

        /// <summary>
        /// The band classes (ADR 0026) on the L*s as it is; or the sky path, with the reason, when
        /// the band or the sky outside it is missing, or when the field is narrower than the band
        /// (ADR 0037).
        /// </summary>
        /// <param name="map">
        /// The map, with its grid and galactic latitudes; its classes, models, method and reason
        /// are written.
        /// </param>
        /// <param name="L">
        /// The L* of each patch of the image as it is, in the map's row order.
        /// </param>
        static void Classify(DustMap map, float[] L)
        {
            int cols = map.Cols, rows = map.Rows, n = cols * rows;
            var band = new bool[n];
            int inBand = 0;
            for (int i = 0; i < n; i++)
            {
                band[i] = Math.Abs(map.GalacticLatitude[i]) < Thresholds.BandGalacticLatitudeDeg;
                if (band[i]) inBand++;
            }
            if (inBand < Thresholds.BandMinFraction * n) { ClassifyBySky(map, L, SkyReason.NoBand); return; }
            if (n - inBand < Thresholds.BandMinFraction * n) { ClassifyBySky(map, L, SkyReason.AllBand); return; }
            if (map.LatitudeSpanDeg < Thresholds.BandMinLatitudeSpanDeg) { ClassifyBySky(map, L, SkyReason.NarrowField); return; }

            float[] sky = SkyModel(L, band, cols, rows, 50);
            float[] level = Smooth(MedianFilter(L, cols, rows, Thresholds.BandLocalWindowPatches), cols, rows, Thresholds.BandLocalSmoothPatches);
            float peak = PeakLevel(L);

            double k = Thresholds.DustDepthLstar, kb = Thresholds.BrightCloudLstar;
            var cls = map.Classes;
            for (int i = 0; i < n; i++)
            {
                float l = L[i];
                PatchClass c = band[i] ? PatchClass.Band : PatchClass.Sky;
                if (!band[i] && l > sky[i] + kb) c = PatchClass.Band;
                if ((band[i] && l < level[i] - k) || l < sky[i] - k) c = PatchClass.Dust;
                if (band[i] && l > level[i] + kb) c = PatchClass.BrightCloud;
                if (l > peak) c = PatchClass.Peak;
                cls[i] = c;
            }
            map.SkyModel = sky;
            map.BandLevel = level;
            map.Method = DustMethod.Band;
            map.SkyReason = SkyReason.None;
        }

        /// <summary>
        /// The classes relative to the modelled sky (ADR 0033): the sky from all the patches,
        /// with the low percentile per cell; the residual patch - sky, with the robust sigma after
        /// one cleaning pass at SkyNoiseClipK sigmas around the median; dust under the modelled
        /// sky by SkyNoiseK sigmas, nebula over it by as much, bright nebula over the Otsu
        /// threshold of the nebula's tail taken WITHOUT the peaks and with at least
        /// BrightClassMinTailFraction of the tail above the threshold by rank (otherwise two
        /// stars would be the upper "class" and the bright nebula would stay empty; none when the
        /// tail has too few patches; tail values tied at the split leave fewer above it), the
        /// peaks the brightest PeakFraction of the patches, the sky the rest. The thresholds stay
        /// on the map, for the legend and the report.
        /// With sigma 0 (a posterised sky, more than half of the patches at one value) the noise
        /// threshold is 0: a residual under 0 is dust, one over 0 is nebula or, over the Otsu
        /// threshold of those, bright nebula.
        /// </summary>
        /// <param name="map">
        /// The map, with its grid; its classes, sky model, thresholds, method and reason are
        /// written, and its band level set to null.
        /// </param>
        /// <param name="L">
        /// The L* of each patch of the image as it is, in the map's row order.
        /// </param>
        /// <param name="reason">Why not the band; written as the map's SkyReason.</param>
        static void ClassifyBySky(DustMap map, float[] L, SkyReason reason)
        {
            int cols = map.Cols, rows = map.Rows, n = cols * rows;
            float[] sky = SkyModel(L, new bool[n], cols, rows, Thresholds.SkyModelLowPercentile);
            var resid = new float[n];
            for (int i = 0; i < n; i++) resid[i] = L[i] - sky[i];

            double sigma = Stats.RobustSigma(resid, out double med);
            float[] kept = resid.Where(v => Math.Abs(v - med) <= Thresholds.SkyNoiseClipK * sigma).ToArray();
            if (kept.Length > 0) sigma = Stats.RobustSigma(kept, out _);

            double noise = Thresholds.SkyNoiseK * sigma;
            float peak = PeakLevel(L);
            var tail = new List<float>();
            for (int i = 0; i < n; i++) if (resid[i] > noise && L[i] <= peak) tail.Add(resid[i]);
            int minAbove = (int)Math.Ceiling(Thresholds.BrightClassMinTailFraction * tail.Count);
            double bright = Stats.OtsuThreshold(Stats.Sorted(tail.ToArray()), minAbove);
            if (double.IsNaN(bright)) bright = double.PositiveInfinity;

            var cls = map.Classes;
            for (int i = 0; i < n; i++)
            {
                double v = resid[i];
                PatchClass c = v < -noise ? PatchClass.Dust : v > bright ? PatchClass.BrightCloud : v > noise ? PatchClass.Band : PatchClass.Sky;
                if (L[i] > peak) c = PatchClass.Peak;
                cls[i] = c;
            }
            map.SkyModel = sky;
            map.BandLevel = null;
            map.Method = DustMethod.Sky;
            map.SkyReason = reason;
            map.SkySigma = sigma;
            map.NoiseThreshold = noise;
            map.BrightThreshold = bright;
        }

        /// <summary>
        /// The L* over which a patch is a peak: the (1 - PeakFraction) percentile of the patches.
        /// </summary>
        /// <param name="L">The L* of each patch; at least one.</param>
        /// <returns>The level, in L*; a patch strictly over it is a peak.</returns>
        static float PeakLevel(float[] L) => (float)Stats.PercentileSorted(Stats.Sorted(L), 100 * (1 - Thresholds.PeakFraction));

        /// <summary>
        /// The modelled sky: the <paramref name="percentile"/> percentile of the patches outside
        /// the band on cells of SkyModelCellPatches, the cells without enough sky patches taken
        /// from the nearest one, interpolated linearly to the patches' resolution and smoothed.
        /// With <paramref name="band"/> all false, each cell has all its patches.
        /// </summary>
        /// <param name="L">The L* of each patch, in the map's row order.</param>
        /// <param name="band">
        /// For each patch, whether it lies in the band and is left out of the model.
        /// </param>
        /// <param name="cols">The grid's columns.</param>
        /// <param name="rows">The grid's rows.</param>
        /// <param name="percentile">
        /// The percentile taken in each cell, 0..100: 50 on the band path,
        /// <see cref="Thresholds.SkyModelLowPercentile"/> on the sky path.
        /// </param>
        /// <returns>
        /// The model, an L* per patch, in the map's row order; 0 everywhere when every patch is in
        /// the band.
        /// </returns>
        internal static float[] SkyModel(float[] L, bool[] band, int cols, int rows, double percentile)
        {
            int cell = Thresholds.SkyModelCellPatches;
            int gw = (cols + cell - 1) / cell, gh = (rows + cell - 1) / cell;
            var grid = new float[gw * gh];
            var known = new bool[gw * gh];
            for (int gy = 0; gy < gh; gy++)
                for (int gx = 0; gx < gw; gx++)
                {
                    var v = new List<float>();
                    for (int by = gy * cell; by < Math.Min(rows, (gy + 1) * cell); by++)
                        for (int bx = gx * cell; bx < Math.Min(cols, (gx + 1) * cell); bx++)
                            if (!band[by * cols + bx]) v.Add(L[by * cols + bx]);
                    if (v.Count >= Thresholds.SkyModelCellMinPatches) { grid[gy * gw + gx] = (float)Stats.PercentileSorted(Stats.Sorted(v.ToArray()), percentile); known[gy * gw + gx] = true; }
                }
            // No cell with enough patches (a small raster): all the sky patches together.
            if (!known.Any(k => k))
            {
                var all = new List<float>();
                for (int i = 0; i < L.Length; i++) if (!band[i]) all.Add(L[i]);
                float v = all.Count > 0 ? (float)Stats.PercentileSorted(Stats.Sorted(all.ToArray()), percentile) : 0;
                for (int g = 0; g < grid.Length; g++) { grid[g] = v; known[g] = true; }
            }
            // The unknown cells take the value of the nearest known one (distance on the grid).
            for (int g = 0; g < grid.Length; g++)
            {
                if (known[g]) continue;
                int gx = g % gw, gy = g / gw; double best = double.MaxValue; float val = 0;
                for (int k2 = 0; k2 < grid.Length; k2++)
                {
                    if (!known[k2]) continue;
                    int kx = k2 % gw, ky = k2 / gw;
                    double d = (kx - gx) * (kx - gx) + (ky - gy) * (ky - gy);
                    if (d < best) { best = d; val = grid[k2]; }
                }
                grid[g] = val;
            }
            // Bilinear interpolation between the cells' centres, then smoothing.
            var o = new float[cols * rows];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    double fx = (bx + 0.5) / cell - 0.5, fy = (by + 0.5) / cell - 0.5;
                    int x0 = Math.Clamp((int)Math.Floor(fx), 0, gw - 1), y0 = Math.Clamp((int)Math.Floor(fy), 0, gh - 1);
                    int x1 = Math.Min(x0 + 1, gw - 1), y1 = Math.Min(y0 + 1, gh - 1);
                    double tx = Math.Clamp(fx - x0, 0, 1), ty = Math.Clamp(fy - y0, 0, 1);
                    double top = grid[y0 * gw + x0] * (1 - tx) + grid[y0 * gw + x1] * tx;
                    double bottom = grid[y1 * gw + x0] * (1 - tx) + grid[y1 * gw + x1] * tx;
                    o[by * cols + bx] = (float)(top * (1 - ty) + bottom * ty);
                }
            return Smooth(o, cols, rows, Thresholds.SkyModelSmoothPatches);
        }

        /// <summary>
        /// The median over a square window of <paramref name="window"/> patches, cut at the edges.
        /// </summary>
        /// <param name="L">The values, one per patch, in row order.</param>
        /// <param name="cols">The grid's columns.</param>
        /// <param name="rows">The grid's rows.</param>
        /// <param name="window">
        /// The window's side, in patches, odd; an even side is taken as the next odd one.
        /// </param>
        /// <returns>The filtered values, one per patch, in row order.</returns>
        internal static float[] MedianFilter(float[] L, int cols, int rows, int window)
        {
            int r = window / 2;
            var o = new float[L.Length];
            var buf = new List<float>(window * window);
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    buf.Clear();
                    for (int y = Math.Max(0, by - r); y <= Math.Min(rows - 1, by + r); y++)
                        for (int x = Math.Max(0, bx - r); x <= Math.Min(cols - 1, bx + r); x++)
                            buf.Add(L[y * cols + x]);
                    // The median by selection (Stats.Median), not by a sort of each window.
                    o[by * cols + bx] = (float)Stats.Median(buf.ToArray());
                }
            return o;
        }

        /// <summary>
        /// The mean over a window of radius <paramref name="radius"/> patches, cut at the edges:
        /// the smoothing.
        /// </summary>
        /// <param name="L">The values, one per patch, in row order.</param>
        /// <param name="cols">The grid's columns.</param>
        /// <param name="rows">The grid's rows.</param>
        /// <param name="radius">
        /// The window's radius, in patches: its side is 2 x radius + 1; 0 copies the values.
        /// </param>
        /// <returns>The smoothed values, one per patch, in row order.</returns>
        internal static float[] Smooth(float[] L, int cols, int rows, int radius)
        {
            var o = new float[L.Length];
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    double sum = 0; int count = 0;
                    for (int y = Math.Max(0, by - radius); y <= Math.Min(rows - 1, by + radius); y++)
                        for (int x = Math.Max(0, bx - radius); x <= Math.Min(cols - 1, bx + radius); x++) { sum += L[y * cols + x]; count++; }
                    o[by * cols + bx] = (float)(sum / count);
                }
            return o;
        }

        /// <summary>
        /// The L* of the mean linear luminance of each whole patch, in the map's row order, on a
        /// raster ALREADY subsampled at the map's step, as it is: the exact path. The grid is the
        /// one Build builds for the same image.
        /// </summary>
        /// <param name="sub">The subsample, 0..1, at the map's step.</param>
        /// <returns>
        /// One L* (0..100) per whole patch, in the map's row order, as
        /// <see cref="PatchLstars{T}(Raster, T)"/> gives it with an
        /// <see cref="ExactLinearizer"/>.
        /// </returns>
        internal static float[] PatchLstars(Raster sub) => PatchLstars(sub, new ExactLinearizer());

        /// <summary>
        /// The same L* per patch, with the samples passed through <paramref name="lin"/>: exact,
        /// or lifted with a gamma and linearised from the table (LiftTable), as the simulation
        /// re-measures them at every step, without an intermediate lifted raster (ADR 0035).
        /// </summary>
        /// <typeparam name="T">The path, a struct the JIT specialises the loop on.</typeparam>
        /// <param name="sub">The subsample, 0..1, at the map's step.</param>
        /// <param name="lin">
        /// The path: an <see cref="ExactLinearizer"/> or a <see cref="LiftTable"/>.
        /// </param>
        /// <returns>
        /// One L* (0..100) per whole patch, in the map's row order: of the mean of the Rec. 709
        /// luminance of the linearised components on colour, of the mean linear value on mono.
        /// </returns>
        internal static float[] PatchLstars<T>(Raster sub, T lin) where T : struct, ISrgbLinearizer
        {
            var (cols, rows, side) = SubsampleGrid(sub.W, sub.H);
            var o = new float[cols * rows];
            bool color = sub.IsColor;
            float[] R = sub.Planes[0], G = color ? sub.Planes[1] : null, B = color ? sub.Planes[2] : null;
            for (int by = 0; by < rows; by++)
                for (int bx = 0; bx < cols; bx++)
                {
                    double sum = 0;
                    for (int y = by * side; y < (by + 1) * side; y++)
                    {
                        int row = y * sub.W;
                        for (int x = bx * side; x < (bx + 1) * side; x++)
                        {
                            int i = row + x;
                            sum += color
                                ? ColorMath.Luminance(lin.Linear(R[i]), lin.Linear(G[i]), lin.Linear(B[i]))
                                : lin.Linear(R[i]);
                        }
                    }
                    o[by * cols + bx] = (float)ColorMath.LstarOfY(sum / (side * side));
                }
            return o;
        }

        /// <summary>
        /// The patches of the dust class and of the sky class; the sky is all the patches when
        /// the sky class is empty (DustMap.SkyClassEmpty).
        /// </summary>
        /// <param name="patchLstars">The L* of each patch, in the map's row order.</param>
        /// <param name="map">The map, for its classes.</param>
        /// <returns>The L*s of the two sets, each sorted in ascending order.</returns>
        static (float[] DustSorted, float[] SkySorted) ClassSets(float[] patchLstars, DustMap map)
        {
            var dustL = new List<float>(); var skyL = new List<float>();
            for (int i = 0; i < patchLstars.Length; i++)
            {
                if (map.Classes[i] == PatchClass.Dust) dustL.Add(patchLstars[i]);
                else if (map.Classes[i] == PatchClass.Sky) skyL.Add(patchLstars[i]);
            }
            return (Stats.Sorted(dustL.ToArray()), Stats.Sorted(skyL.Count > 0 ? skyL.ToArray() : patchLstars));
        }

        /// <summary>
        /// What gives the dust's L*, on the L*s as it is (ADR 0033): the dust class when it holds
        /// at least DustClassMinFraction of the patches and its DustClassFraction percentile is
        /// at or under the SkyDarkestFraction percentile of the sky class; otherwise the darkest
        /// sky. Under that threshold the dust class is cores or artefacts and does not decide.
        /// Decided once, so that every row of the ladder measures the same dust as the definition.
        /// </summary>
        /// <param name="asIsPatchLstars">
        /// The L* of each patch of the image as it is, in the map's row order.
        /// </param>
        /// <param name="map">The map, for its classes.</param>
        /// <returns>The source, which Build keeps as <see cref="DustMap.DustSource"/>.</returns>
        internal static DustSource DecideDustSource(float[] asIsPatchLstars, DustMap map)
        {
            var (dust, sky) = ClassSets(asIsPatchLstars, map);
            if (dust.Length < Thresholds.DustClassMinFraction * asIsPatchLstars.Length) return DustSource.DarkestSky;
            double dustClass = Stats.PercentileSorted(dust, Thresholds.DustClassFraction * 100);
            double darkestSky = Stats.PercentileSorted(sky, Thresholds.SkyDarkestFraction * 100);
            return dustClass <= darkestSky ? DustSource.DustClass : DustSource.DarkestSky;
        }

        /// <summary>
        /// The dust, the sky and "negru pur" on the patches' L*s, on the map's classes, on either
        /// path: the sky is the median of the sky class (of all the patches when the class is
        /// empty); the dust is the statistic DecideDustSource chose on the image as it is: the
        /// DustClassFraction percentile of the dust class, or the SkyDarkestFraction percentile
        /// of the sky class.
        /// </summary>
        /// <param name="patchLstars">
        /// The L* of each patch, as it is or lifted, in the map's row order.
        /// </param>
        /// <param name="map">The map: its classes, its dust source and its clouds.</param>
        /// <param name="paperBlackLstar">
        /// The paper's black, in L*; a patch strictly under it counts as "negru pur".
        /// </param>
        /// <returns>
        /// The dust's and the sky's L*, the share of the patches under paper black, each cloud's
        /// L* and the dust class's size; the patch L*s are kept, not copied.
        /// </returns>
        internal static DustStats Measure(float[] patchLstars, DustMap map, double paperBlackLstar)
        {
            int n = patchLstars.Length;
            int below = 0;
            for (int i = 0; i < n; i++) if (patchLstars[i] < paperBlackLstar) below++;

            var (dustSorted, skySorted) = ClassSets(patchLstars, map);
            double sky = Stats.PercentileSorted(skySorted, 50);
            double dust = map.DustSource == DustSource.DustClass
                ? Stats.PercentileSorted(dustSorted, Thresholds.DustClassFraction * 100)
                : Stats.PercentileSorted(skySorted, Thresholds.SkyDarkestFraction * 100);

            var cloudLstars = new double[map.Clouds.Count];
            for (int c = 0; c < map.Clouds.Count; c++)
            {
                int[] idx = map.Clouds[c].Patches;
                var v = new float[idx.Length];
                for (int k = 0; k < idx.Length; k++) v[k] = patchLstars[idx[k]];
                cloudLstars[c] = Stats.PercentileSorted(Stats.Sorted(v), Thresholds.DustCloudFraction * 100);
            }

            return new DustStats
            {
                LstarDust = dust,
                LstarSky = sky,
                PctPatchesBelowPaperBlack = 100.0 * below / n,
                PatchLstars = patchLstars,
                CloudLstars = cloudLstars,
                DustPatchCount = dustSorted.Length,
            };
        }
    }
}
