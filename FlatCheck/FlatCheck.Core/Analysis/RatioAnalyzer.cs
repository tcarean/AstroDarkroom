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

// RatioAnalyzer.cs - mode 2: ATTRIBUTION by 180-degree rotation.
//
// WHAT THE MODE ASKS, and why the question changed. Until this rewrite the mode answered
// "is the flats setup clean?" and gave OK / SUSPECT / PROBLEMA. It could not.
// Measured on three real pairs of masters, with geometry known for certain:
//
//   - two stacks rotated correctly, with a real sky gradient -> SUSPECT, "fix the source".
//     But the gradient had already been cancelled from the master by that very rotation.
//     Correct as a measurement, not actionable as advice.
//   - two stacks rotated correctly, from a session whose master has 11% of non-radial
//     structure -> OK, "you can build the library on it". The defect was fixed to the camera,
//     and to what is fixed to the camera this test is blind by construction.
//   - two stacks from the SAME orientation, so with no test performed at all -> OK all the
//     same, "you can build the library on it".
//
// Two false greens out of three. The cause is not the calibration of the thresholds: two stacks
// from the same orientation and two rotated stacks from a truly clean setup produce statistically
// IDENTICAL data - in both cases A/B is noise only. The information that would tell them apart
// is not in the pixels, so no threshold can extract it.
//
// What can be computed from the two files, though, and what no other mode can:
//
//     S = (A + B) / 2     the part fixed to the CAMERA - the external gradient cancels,
//                         because in B it is reversed. This IS the master you get by
//                         stacking both orientations, so its verdict is the verdict of
//                         your flat. (Checked: 4.02% computed here vs 4.00%
//                         measured on pp_flat_stacked.fit with all 30 frames.)
//
//     W = A / B           the part fixed in the WORLD, at double amplitude. Informative: it says
//                         how much the rotation took out of your master, not whether the setup
//                         is good.
//
// So the mode no longer gives a verdict on the setup. It gives the STRUCTURE verdict on S - the
// same battery as mode 1, reused whole - and reports W as information.
//
// THE ERROR BAR, and why it is not calibrated. log(A/B) must be exactly antisymmetric under
// a 180-degree rotation: a contamination fixed in the world goes ENTIRELY into the odd part and
// NOT AT ALL into the even one. The noise splits in two with equal variance. So the even part
// is an unbiased estimator of the noise in the odd part, built from the same data,
// without any assumption and without any constant to tune. Validated on a pair without
// rotation, where the odd/even ratio came out 1.17x, against 2.59x on a rotated one.
//
// WHAT THE MODE CANNOT DO, and must say in the report because it does not show in the figures:
//   - it cannot check from the pixels that you rotated between the two stacks;
//   - it cannot say whether the part fixed to the camera is the lens (harmless, it divides out
//     correctly from the lights) or something ON the lens - a strap, condensation, a crooked
//     lens hood - which rotates with the camera and looks identical, but is no longer there
//     for the lights.
//
// Pure: Img x Img -> RatioResult. The text is in Report/RatioReport.cs.

using System;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// What <see cref="RatioAnalyzer.Analyze"/> measured on a pair of masters: the structure
    /// of their mean (the verdict) and the world-fixed component of their ratio (information).
    /// </summary>
    internal sealed class RatioResult
    {
        /// <summary>
        /// The structure of the combined master (A+B)/2, that is of the part fixed to the camera.
        /// The mode's verdict comes from here.
        /// </summary>
        internal StructureResult Sensor;

        internal double WorldPtpPct;      // external gradient PER ORIENTATION (odd / 2)
        internal double WorldErrPct;      // its error bar (even / 2)
        internal double WorldCornerPct;   // the corner spread of W, per orientation
        internal string WorldLabel;       // informative label on Th.RatioOk/Bad

        internal Img World;               // A/B normalised to median 1, for the map

        // Metadata from the FITS headers, filled by RatioRun. 0 / null = unknown.
        // They are not measurements - they are the only place in the report where one can see
        // whether the two stacks can really come from two different orientations.
        internal int StackCountA, StackCountB;
        internal string DateObsA, DateObsB;

        internal string Verdict => Sensor.Verdict;
    }

    /// <summary>
    /// Mode 2, <c>--ratio</c>: splits two masters shot 180 degrees apart into the part fixed to
    /// the camera, (A+B)/2, and the part fixed in the world, A/B (ADR 0019).
    /// </summary>
    internal static class RatioAnalyzer
    {
        /// <summary>
        /// Prepares and normalises both masters, runs the structure battery on their mean and
        /// measures the world-fixed component of their ratio with its error bar, the latter
        /// labelled on <see cref="Th.RatioOk"/> / <see cref="Th.RatioBad"/>. Throws when the
        /// two prepared rasters differ in size.
        /// </summary>
        /// <param name="rawA">The first master, as loaded.</param>
        /// <param name="rawB">The second master, shot after a 180-degree rotation, as
        /// loaded.</param>
        /// <returns>The camera-fixed structure and the world-fixed figures, in percent per
        /// orientation; the header metadata are left for the caller.</returns>
        internal static RatioResult Analyze(Img rawA, Img rawB)
        {
            Img a = Img.Prepare(rawA);
            Img b = Img.Prepare(rawB);
            if (a.H != b.H || a.W != b.W)
                throw new Exception("Cele doua imagini au dimensiuni diferite.");

            // Normalising each to median 1 before averaging is not cosmetic: it is
            // what Siril does with "multiplicative normalized input". Without it, on a
            // twilight sky that drops 12% between the two stacks, the mean would weigh A more
            // than B and the gradient would no longer cancel completely.
            Normalise(a);
            Normalise(b);

            // ---- the part fixed to the camera: the mean of the two orientations ----
            var s = new Img(a.H, a.W);
            for (int i = 0; i < s.D.Length; i++) s.D[i] = (a.D[i] + b.D[i]) / 2;

            // Already prepared; Prepare being linear, the mean of the prepared rasters is the
            // same thing as preparing the mean, but at a sixteenth of the memory on the D3300's
            // 6016 x 4016 masters (1504 x 1004 prepared).
            var sensor = StructureAnalyzer.AnalyzePrepared(s);

            // ---- the part fixed in the world: the ratio ----
            Img world = Img.Divide(a, b);
            Normalise(world);

            var (odd, even) = SplitByRotation(world);

            // Divided by 2: W carries the contamination at double amplitude, because in B it is
            // reversed. The figures reported are per orientation, that is how much is in each
            // master taken separately.
            double worldPtp = Ptp(odd) / 2 * 100;
            double worldErr = Ptp(even) / 2 * 100;

            var (cor, _) = Metrics.CornerStats(world);
            double cmean = cor.Values.Average();
            double worldCorner = (cor.Values.Max() - cor.Values.Min()) / cmean * 100 / 2;

            return new RatioResult
            {
                Sensor = sensor,
                WorldPtpPct = worldPtp,
                WorldErrPct = worldErr,
                WorldCornerPct = worldCorner,
                WorldLabel = Stats.Verdict(worldPtp, Th.RatioOk, Th.RatioBad),
                World = world,
            };
        }

        /// <summary>Divides a raster in place by its median, so the median becomes 1.</summary>
        /// <param name="img">The raster; left unchanged when its median is 0 or below.</param>
        static void Normalise(Img img)
        {
            double med = Stats.Percentile(img.D, 50);
            if (med <= 0) return;
            for (int i = 0; i < img.D.Length; i++) img.D[i] /= med;
        }

        /// <summary>
        /// Decomposes log(W) into the part that reverses under the 180-degree rotation and the
        /// one that does not. On the tile grid, not on pixels: without the averaging over tiles
        /// both halves would be dominated by photon noise, which splits equally, and their
        /// ratio would always be 1 whatever the signal.
        ///
        /// In log, not on the ratio directly: x and 1/x are not antisymmetric, only the log is.
        /// At these amplitudes the difference is of the order of 0.02%, but the log is the
        /// correct one and costs nothing.
        /// </summary>
        /// <param name="world">The ratio A/B, normalised to median 1.</param>
        /// <returns>The odd and the even part of log(W), on the tile grid.</returns>
        static (Img odd, Img even) SplitByRotation(Img world)
        {
            var log = new Img(world.H, world.W);
            for (int i = 0; i < log.D.Length; i++)
                // A clamp at both ends: Divide guards only the denominator, so a null pixel
                // in the numerator would give a wild log that would drag its tile's mean.
                log.D[i] = Math.Log(Math.Min(Math.Max(world.D[i], 0.01), 100.0));

            // A CENTRED crop before the tiles, so that the 180-degree rotation falls exactly on
            // the grid. BlockMean drops the partial blocks only at the end - see
            // Img.CropCentred for what it costs if not corrected.
            int k = Metrics.TileSize(log);
            Img t = Img.BlockMean(Img.CropCentred(log, log.H / k * k, log.W / k * k), k);
            Img r = Img.Rotate180(t);

            var odd = new Img(t.H, t.W);
            var even = new Img(t.H, t.W);
            for (int i = 0; i < t.D.Length; i++)
            {
                odd.D[i] = (t.D[i] - r.D[i]) / 2;
                even.D[i] = (t.D[i] + r.D[i]) / 2;
            }
            return (odd, even);
        }

        /// <summary>
        /// The robust peak-to-peak of a tile map: its 95th minus its 5th percentile.
        /// </summary>
        /// <param name="t">The tile map.</param>
        /// <returns>The spread, in the map's units; NaN for fewer than 10 tiles.</returns>
        static double Ptp(Img t)
            => t.D.Length < 10
                ? double.NaN
                : Stats.Percentile(t.D, 95) - Stats.Percentile(t.D, 5);
    }
}
