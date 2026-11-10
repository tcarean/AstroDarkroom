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

// StructureAnalyzer.cs - mode 1: is the master flat radially symmetric?
//
// Pure: Img -> StructureResult. It writes nothing, it reads no files. The text is in
// Report/StructureReport.cs.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// What the structure battery measured on one master: the five deviation tests with their
    /// verdicts, the informative figures (centre offset, vignetting depth, local spot) and the
    /// residual map.
    /// </summary>
    internal sealed class StructureResult
    {
        internal int Width, Height;                  // the analysis raster (after Prepare)
        internal double RawMedian, RawMax;           // on the original raster, informative
        internal double FitCx, FitCy;                // the fitted illumination vertex
        internal double SensorCx, SensorCy;          // the sensor's centre, ((w-1)/2, (h-1)/2) (R-014)
        internal double CentroidOffsetPct;           // % of the half-diagonal, INFORMATIVE
        internal bool CentroidValid;                 // false => the fitted vertex is not usable
        internal double TiltPct;                     // the amplitude of the linear term, %
        internal Dictionary<string, double> Corners; // the medians of the 4 corners
        internal double CornerMean, CentreMedian;
        internal double VignetteStops;               // informative, NOT a test
        internal double CornerSpreadPct;
        internal double AzimuthalMaxPct;
        internal double ResidualPtpPct;
        internal double MaxTilePct;                  // the most deviant tile, over the whole frame
        internal double LocalSpotPct;                // the deepest compact spot, SIGNED
        internal int LocalSpotX, LocalSpotY;         // where it is, in the analysis raster's pixels
        internal bool LocalSpotFlagged;              // |LocalSpotPct| >= Th.LocalSpotWarn
        internal Img Residual;                       // img / radial model, for the map
        internal string CornerVerdict, AzimuthalVerdict, ResidualVerdict, TiltVerdict, MaxTileVerdict;
        internal string Verdict;                     // the worst of the five
    }

    /// <summary>
    /// Mode 1, structure: the five deviation tests of a master against radial symmetry, each
    /// read on the residual scale (ADR 0015).
    /// </summary>
    internal static class StructureAnalyzer
    {
        /// <summary>Prepares a master and runs the structure battery on it.</summary>
        /// <param name="raw">The master, as loaded; not modified.</param>
        /// <returns>The figures and the verdicts; RawMedian/RawMax in the file's units.</returns>
        internal static StructureResult Analyze(Img raw) => Run(raw, Img.Prepare(raw));

        /// <summary>
        /// The same analysis, on a raster already passed through <see cref="Img.Prepare"/>.
        ///
        /// It exists for the attribution mode, which builds its own input - the mean of the
        /// two orientations - and would pay the memory twice if it had to hold it at full
        /// resolution. Prepare being only block means, it is linear: the mean of the prepared
        /// rasters is the same thing as preparing the mean, at a sixteenth of the memory on the
        /// D3300's 6016 x 4016 masters ((2k)^2 = 16 with k = 2: 1504 x 1004 prepared).
        ///
        /// The comparison of two flats (--against) calls it too, for the batteries on A, B and
        /// their ratio.
        ///
        /// RawMedian/RawMax then come from the prepared raster, so they no longer mean ADU. They
        /// are not printed: the comparison report shows the five tests, the verdicts and the
        /// residual map, not that row.
        /// </summary>
        /// <param name="prepared">The analysis raster; not modified.</param>
        /// <returns>The figures and the verdicts.</returns>
        internal static StructureResult AnalyzePrepared(Img prepared) => Run(prepared, prepared);

        /// <summary>
        /// The battery: the corners, the azimuthal asymmetry, the residual after the radial
        /// model, the tilt and the worst tile, each through <see cref="Stats.ResidualVerdict"/>,
        /// plus the local spot against <see cref="Th.LocalSpotWarn"/>.
        /// </summary>
        /// <param name="raw">The raster RawMedian and RawMax are taken from.</param>
        /// <param name="img">The analysis raster every test runs on.</param>
        /// <returns>The figures and the verdicts; the overall one the worst of the five.</returns>
        static StructureResult Run(Img raw, Img img)
        {
            int h = img.H, w = img.W;

            // WARNING: the radial model and the asymmetry are computed around the SENSOR's
            // centre, not the fitted vertex. Centring on the vertex would absorb exactly the
            // linear gradient being looked for, and the test would cancel itself.
            double cx = Metrics.Centre(w), cy = Metrics.Centre(h);
            // Two things come out of the same fit: the tilt, which is a test, and the vertex's
            // offset, which is only informative - it is not scale-invariant, and above a
            // threshold not even monotonic. See Metrics.FitCenter for why.
            var (fx, fy, tilt, centroidValid) = Metrics.FitCenter(img);
            double off = Math.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy))
                       / Math.Sqrt(cx * cx + cy * cy) * 100;

            var (cor, ctr) = Metrics.CornerStats(img);
            double cmean = cor.Values.Average();
            double vignStops = Math.Log2(ctr / cmean);
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            // The profile is taken from the raster SYMMETRISED at 180 degrees, not from img:
            // otherwise the median per ring jumps beyond the inscribed circle and the map gets
            // concentric rings that are not in the flat. See Img.Symmetrise for the mechanism.
            // The residual stays img's, though - symmetrising would cancel the very gradient
            // the map must show.
            var (_, _, model) = Metrics.RadialModel(Img.Symmetrise(img), cx, cy);
            var resid = Img.Divide(img, model);
            // Over the whole frame, not only in the inscribed circle - see Metrics.LargeScalePtp
            // for what the restriction costs and why it is no longer needed.
            double resPtp = Metrics.LargeScalePtp(resid) * 100;
            // The residual trims 5% of the tiles at each end, so a compact defect escapes it
            // completely. The most deviant tile catches it; it is the fifth test.
            double maxTile = Metrics.MaxTileDeviation(resid) * 100;
            // And, separately, what is small AND fixed to the camera - dust, a ring, a small leak -
            // after the large-scale structure is taken out. It does not vote: the answer depends
            // on what you do with the flat (session vs library), so it gives a warning.
            var (spot, spotX, spotY) = Metrics.LocalSpots(resid);
            double spotPct = spot * 100;

            var az = Metrics.AzimuthalAsymmetry(img, cx, cy);
            double azMax = az.Count > 0 ? az.Max(v => v.ptp) : double.NaN;

            // All five measure the same departure from radial symmetry, from different
            // angles, and all five are fractional amplitudes in percent - so they share the
            // residual scale. The centre offset measures something else, a position, and
            // gives no verdict at all: it is reported as information.
            string vCorner = Stats.ResidualVerdict(spread);
            string vAzim = Stats.ResidualVerdict(azMax);
            string vResid = Stats.ResidualVerdict(resPtp);
            string vTilt = Stats.ResidualVerdict(tilt);
            string vMaxTile = Stats.ResidualVerdict(maxTile);

            return new StructureResult
            {
                Width = w,
                Height = h,
                RawMedian = Stats.Percentile(raw.D, 50),
                RawMax = raw.D.Max(),
                FitCx = fx,
                FitCy = fy,
                SensorCx = cx,
                SensorCy = cy,
                CentroidOffsetPct = off,
                CentroidValid = centroidValid,
                TiltPct = tilt,
                Corners = cor,
                CornerMean = cmean,
                CentreMedian = ctr,
                VignetteStops = vignStops,
                CornerSpreadPct = spread,
                AzimuthalMaxPct = azMax,
                ResidualPtpPct = resPtp,
                MaxTilePct = maxTile,
                LocalSpotPct = spotPct,
                LocalSpotX = spotX,
                LocalSpotY = spotY,
                // !(x < threshold) and not (x >= threshold), so that NaN falls on the warned
                // side - the same convention as Stats.Verdict.
                LocalSpotFlagged = !(Math.Abs(spotPct) < Th.LocalSpotWarn),
                Residual = resid,
                CornerVerdict = vCorner,
                AzimuthalVerdict = vAzim,
                ResidualVerdict = vResid,
                TiltVerdict = vTilt,
                MaxTileVerdict = vMaxTile,
                // the overall verdict is the worst of the five, expressed in the
                // vocabulary of the residual bands
                Verdict = Stats.ResidualBands[
                    new[] { vCorner, vAzim, vResid, vTilt, vMaxTile }.Max(Stats.Severity)],
            };
        }
    }
}
