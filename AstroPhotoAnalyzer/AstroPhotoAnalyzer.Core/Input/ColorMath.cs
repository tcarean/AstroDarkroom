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

// ColorMath.cs - sRGB, luminance and L*: the pure functions everything about paper rests on.
//
// The image is assumed to be sRGB (Siril writes it so, and so was the acceptance image). A
// different profile is reported as a fact (SourceInfo.IccDescription) but not converted: the
// tool measures what the processing wrote, not an interpretation of it.
//
// The formulas are the standard ones (IEC 61966-2-1 for sRGB, CIE 1976 for L*), written here
// once so that two versions with constants rounded differently cannot appear.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The conversions between encoded sRGB, linear values, luminance and CIE L*, and the black
    /// of a paper.
    /// </summary>
    internal static class ColorMath
    {
        /// <summary>Encoded sRGB (0..1) -> linear value (0..1), for one channel.</summary>
        /// <param name="c">The encoded value, 0..1.</param>
        /// <returns>The linear value, 0..1.</returns>
        internal static double SrgbToLinear(double c)
            => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>
        /// Linear value -> encoded sRGB: the inverse of SrgbToLinear, to 3e-8 just under 0.04045.
        /// </summary>
        /// <param name="l">The linear value, 0..1.</param>
        /// <returns>The encoded value, 0..1.</returns>
        internal static double LinearToSrgb(double l)
            => l <= 0.0031308 ? 12.92 * l : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;

        /// <summary>
        /// The luminance Y of LINEAR components, with the Rec. 709 / sRGB weights.
        /// </summary>
        /// <param name="rLin">The linear red, 0..1.</param>
        /// <param name="gLin">The linear green, 0..1.</param>
        /// <param name="bLin">The linear blue, 0..1.</param>
        /// <returns>Y, 0..1.</returns>
        internal static double Luminance(double rLin, double gLin, double bLin)
            => 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;

        /// <summary>
        /// L* (CIE 1976) of the relative luminance Y (0..1, white = 1). The linear segment under
        /// 0.008856 is the standard's; without it, the L* of a very small Y would be negative.
        /// </summary>
        /// <param name="y">The relative luminance, 0..1.</param>
        /// <returns>L*, 0..100.</returns>
        internal static double LstarOfY(double y)
            => y > 0.008856 ? 116 * Math.Cbrt(y) - 16 : 903.3 * y;

        /// <summary>The inverse of LstarOfY: the relative luminance Y (0..1) of an L*.</summary>
        /// <param name="l">L*, 0..100.</param>
        /// <returns>Y, 0..1.</returns>
        internal static double YOfLstar(double l)
            => l > 903.3 * 0.008856 ? Math.Pow((l + 16) / 116, 3) : l / 903.3;

        /// <summary>
        /// The 8-bit level (0..255, fractional) of a grey R = G = B with the given L* - what you
        /// read on Siril's histogram for a threshold given in L*.
        /// </summary>
        /// <param name="l">L*, 0..100.</param>
        /// <returns>The level, 0..255, not rounded.</returns>
        internal static double GrayLevelOfLstar(double l)
            => LinearToSrgb(YOfLstar(l)) * 255;

        /// <summary>The L* of a grey R = G = B at the given 8-bit level.</summary>
        /// <param name="level">The level, 0..255; a fraction is allowed.</param>
        /// <returns>L*, 0..100.</returns>
        internal static double LstarOfGrayLevel(double level)
            => LstarOfY(SrgbToLinear(level / 255));

        /// <summary>
        /// The L* of an sRGB pixel. On mono the three components are the same value and the
        /// weights add up to 1, so the formula is the same.
        /// </summary>
        /// <param name="r">The encoded red, 0..1.</param>
        /// <param name="g">The encoded green, 0..1.</param>
        /// <param name="b">The encoded blue, 0..1.</param>
        /// <returns>L*, 0..100.</returns>
        internal static double LstarOfSrgb(double r, double g, double b)
            => LstarOfY(Luminance(SrgbToLinear(r), SrgbToLinear(g), SrgbToLinear(b)));

        /// <summary>
        /// The CIE L*a*b* of an sRGB pixel, D65: the sRGB matrix to XYZ, the white taken as the
        /// matrix's own row sums (0.9505, 1, 1.0890; the standard's 0.95047 and 1.08883 differ in
        /// the fourth decimal and would give a grey an a* of 0.01), the cube root with the
        /// standard's linear segment under 0.008856. a* is negative towards green and positive
        /// towards magenta, b* negative towards blue and positive towards yellow; a grey has
        /// both at exactly 0.
        /// </summary>
        /// <param name="r">The encoded red, 0..1.</param>
        /// <param name="g">The encoded green, 0..1.</param>
        /// <param name="b">The encoded blue, 0..1.</param>
        /// <returns>L* (0..100), a* and b*.</returns>
        internal static (double L, double A, double B) LabOfSrgb(double r, double g, double b)
        {
            double rl = SrgbToLinear(r), gl = SrgbToLinear(g), bl = SrgbToLinear(b);
            double x = (0.4124 * rl + 0.3576 * gl + 0.1805 * bl) / 0.9505;
            double y = 0.2126 * rl + 0.7152 * gl + 0.0722 * bl;
            double z = (0.0193 * rl + 0.1192 * gl + 0.9505 * bl) / 1.0890;
            double fx = LabF(x), fy = LabF(y), fz = LabF(z);
            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        /// <summary>
        /// The CIE L*a*b* of a tristimulus value against a given white: the same companding as
        /// <see cref="LabOfSrgb"/>, for a colour already in XYZ (the soft proof's, relative to
        /// D50, R-160).
        /// </summary>
        /// <param name="x">The colour's X, in the white's scale.</param>
        /// <param name="y">The colour's Y, in the white's scale.</param>
        /// <param name="z">The colour's Z, in the white's scale.</param>
        /// <param name="xn">The white's X, positive.</param>
        /// <param name="yn">The white's Y, positive.</param>
        /// <param name="zn">The white's Z, positive.</param>
        /// <returns>L* (0..100 for a colour under the white), a* and b*.</returns>
        internal static (double L, double A, double B) LabOfXyz(double x, double y, double z, double xn, double yn, double zn)
        {
            double fx = LabF(x / xn), fy = LabF(y / yn), fz = LabF(z / zn);
            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        /// <summary>The L*a*b* companding function: the cube root, linear under 0.008856.</summary>
        /// <param name="t">A tristimulus value relative to the white, 0..1.</param>
        /// <returns>f(t), 4/29..1.</returns>
        static double LabF(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;

        /// <summary>
        /// The blackest black a medium with the given maximum density can render: the
        /// reflectance 10^-Dmax, in L*. Gloss baryta (Dmax 2.4) -> L* ~3.6; matte canvas (1.5)
        /// -> ~21; a C-print under acrylic (2.2) -> ~6. Everything under this threshold in the
        /// file prints with THE SAME black - the background recommendation starts from here.
        /// </summary>
        /// <param name="dmax">The medium's maximum optical density, positive.</param>
        /// <returns>The L* of the paper's black, 0..100.</returns>
        internal static double PaperBlackLstar(double dmax)
            => LstarOfY(Math.Pow(10, -dmax));
    }
}
