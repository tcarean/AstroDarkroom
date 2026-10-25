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

// LightSuitability.cs - can the --light test say anything about this frame?
//
// The --light mode splits the background into "light-pollution plane" + "radial terms =
// flat error". The split holds as long as the pollution dome is quasi-linear across the
// frame. On a wide field it CURVES inside the frame, and the radially symmetric part of that
// curvature has exactly the shape of vignetting: two things of the same shape cannot be
// separated by a fit, however well written.
//
// It is not a limitation of the implementation, it is a geometric degeneracy. That is why above
// Th.LightFovMax the mode REFUSES to answer, instead of giving a bad answer - exactly the case
// that motivated this file: 20 mm on APS-C (~70 degrees diagonal) reported
// "+3.13 % SUSPECT, SUB-corectie" on a flat that had nothing radially wrong.
//
// The field is derived in layers, in order of confidence. The last layer matters most as a
// design decision: when nothing can be found out, the mode CONTINUES with a note, it does not
// refuse. Otherwise any FITS with a minimal header - including all the synthetic fixtures
// in the tests - would become impossible to analyse.

using System;
using System.Collections.Generic;
using System.Globalization;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// How far the field of view lets <c>--light</c> separate the sky plane from the radial
    /// flat error.
    /// </summary>
    internal enum FovLevel
    {
        Ok,          // narrow field: the plane/radial split is valid
        Marginal,    // can run, but with a warning
        TooWide,     // geometric degeneracy: the test does not apply
        Unknown,     // the header does not say enough; it runs, with a note
    }

    /// <summary>
    /// The answer of <see cref="LightSuitability.Check"/>: the level, the field or focal length
    /// it was decided on, and where that figure came from.
    /// </summary>
    internal sealed class SuitabilityResult
    {
        internal FovLevel Level;
        internal double FovDeg = double.NaN;    // diagonal, degrees. NaN = not computed
        internal double FocalMm = double.NaN;   // only when the decision was taken on the focal length
        internal string Source;                 // where the figure comes from, for the report

        internal bool CanRun => Level != FovLevel.TooWide;
    }

    /// <summary>
    /// Decides from the header (or <c>--fov</c>) whether the field is narrow enough for the
    /// <c>--light</c> test, before <see cref="LightAnalyzer"/> runs.
    /// </summary>
    internal static class LightSuitability
    {
        /// <summary>
        /// Derives the diagonal field of view in layers - <c>--fov</c>, the WCS scale, the focal
        /// length with the pixel size, the focal length alone - and reads it against
        /// <see cref="Th.LightFovOk"/> / <see cref="Th.LightFovMax"/> (or, on the focal length
        /// alone, <see cref="Th.LightFocalOk"/> / <see cref="Th.LightFocalMin"/>).
        /// </summary>
        /// <param name="header">The FITS header of the light, key to raw value; null when there
        /// is none (the level is then Unknown).</param>
        /// <param name="fovOverride">The diagonal field in degrees given with --fov, or
        /// null.</param>
        /// <returns>The level and its source; Unknown when nothing in the header tells.</returns>
        internal static SuitabilityResult Check(
            IReadOnlyDictionary<string, string> header, double? fovOverride)
        {
            // 1. Declared by the person. It beats any autodetection - it exists precisely for the
            //    case where the layers below cannot know (DSLR without XPIXSZ, small sensor).
            if (fovOverride.HasValue)
                return FromFov(fovOverride.Value, "--fov");

            if (header == null) return Unknown();

            int n1 = FitsReader.HInt(header, "NAXIS1", 0);
            int n2 = FitsReader.HInt(header, "NAXIS2", 0);
            if (n1 <= 0 || n2 <= 0) return Unknown();
            double diagPx = Math.Sqrt((double)n1 * n1 + (double)n2 * n2);

            // 2. Plate solve: the scale in degrees/pixel is measured, not deduced. The best
            //    source when it exists. CDELT1 if present, else the norm of the column of the
            //    CD matrix (CD1_1, CD2_1), which is the same thing written differently.
            double scale = Math.Abs(FitsReader.HDbl(header, "CDELT1", 0.0));
            if (scale <= 0)
            {
                double cd11 = FitsReader.HDbl(header, "CD1_1", 0.0);
                double cd21 = FitsReader.HDbl(header, "CD2_1", 0.0);
                if (cd11 != 0 || cd21 != 0) scale = Math.Sqrt(cd11 * cd11 + cd21 * cd21);
            }
            if (scale > 0) return FromFov(diagPx * scale, "WCS");

            double focal = FitsReader.HDbl(header, "FOCALLEN", 0.0);
            if (focal <= 0) return Unknown();

            // 3. Pixel size + focal length: the real field, exact. Siril writes XPIXSZ for
            //    astro cameras. For DSLRs it does not, so this layer is often skipped.
            double xpix = FitsReader.HDbl(header, "XPIXSZ", 0.0);
            double ypix = FitsReader.HDbl(header, "YPIXSZ", xpix);
            if (xpix > 0 && ypix > 0)
            {
                double xbin = Math.Max(1, FitsReader.HInt(header, "XBINNING", 1));
                double ybin = Math.Max(1, FitsReader.HInt(header, "YBINNING", 1));
                double wMm = n1 * xbin * xpix / 1000.0;
                double hMm = n2 * ybin * ypix / 1000.0;
                double diagMm = Math.Sqrt(wMm * wMm + hMm * hMm);
                double fov = 2 * Math.Atan(diagMm / (2 * focal)) * 180.0 / Math.PI;
                return FromFov(fov, $"FOCALLEN={Num(focal)}mm + XPIXSZ");
            }

            // 4. The focal length only. The field cannot be computed - it depends on the sensor -
            //    so NO field figure is reported, since it would be invented. The focal length is
            //    compared with the thresholds computed for the largest plausible sensor
            //    (full-frame), so the judgement is conservative: on a smaller sensor the real
            //    field is narrower than the threshold assumes. For that case there is --fov.
            var level = focal >= Th.LightFocalOk ? FovLevel.Ok
                      : focal >= Th.LightFocalMin ? FovLevel.Marginal
                      : FovLevel.TooWide;
            return new SuitabilityResult
            {
                Level = level,
                FocalMm = focal,
                Source = T($"FOCALLEN={Num(focal)}mm, without the pixel size", $"FOCALLEN={Num(focal)}mm, fara marimea pixelului"),
            };
        }

        /// <summary>The level of a known diagonal field.</summary>
        /// <param name="fovDeg">The diagonal field of view, degrees.</param>
        /// <param name="source">Where the figure came from, for the report.</param>
        /// <returns>The level read against <see cref="Th.LightFovOk"/> and
        /// <see cref="Th.LightFovMax"/>, with the field and its source.</returns>
        static SuitabilityResult FromFov(double fovDeg, string source) => new()
        {
            Level = fovDeg < Th.LightFovOk ? FovLevel.Ok
                  : fovDeg < Th.LightFovMax ? FovLevel.Marginal
                  : FovLevel.TooWide,
            FovDeg = fovDeg,
            Source = source,
        };

        /// <summary>The answer when the header says too little.</summary>
        /// <returns>A result of level Unknown, with no figure and no source.</returns>
        static SuitabilityResult Unknown() => new() { Level = FovLevel.Unknown };

        /// <summary>The focal length without useless decimals: "20mm", not "20.0mm".</summary>
        /// <param name="v">The focal length, mm.</param>
        /// <returns>The number with no decimal when whole, else one decimal.</returns>
        static string Num(double v)
            => v.ToString(Math.Abs(v - Math.Round(v)) < 1e-9 ? "0" : "0.0",
                          CultureInfo.InvariantCulture);
    }
}
