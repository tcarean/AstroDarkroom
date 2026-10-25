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

// The synthetic flat: the raster V(x, y) of a vignetting model, at the size of the reference
// frame, with 1.0 on the axis. Achromatic by construction - the same value in all four
// Bayer positions - because the published models have a single set of parameters for
// all the channels ("identical for all color channels", the Adobe document). Pure: geometry
// and model in, raster out.
//
// Beside the raster, the depth is measured from the model at a few points of the frame - the
// corner (the centre of the 5 % box CornerStats uses), the long edge, the short edge,
// half the half-diagonal - so that the report says how deep the vignetting it writes is,
// in stops, at the same points where the real masters were compared with the two
// databases (docs/design/lens-profiles.md, "6. The flat, the report, the file").

using System;
using System.Collections.Generic;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// A synthetic flat and its depth in stops at the reference points, as
    /// <see cref="SyntheticFlatBuilder.Build"/> made it.
    /// </summary>
    internal sealed class SyntheticFlatResult
    {
        internal readonly Img Flat;
        internal readonly LensProfile Profile;
        /// <summary>
        /// All the lens's profiles (the range of focal lengths and apertures, for the mismatch
        /// warning); null when not known.
        /// </summary>
        internal IReadOnlyList<LensProfile> LensProfiles;
        internal readonly double CameraCrop;
        internal readonly int Width, Height;
        /// <summary>
        /// The depth in stops (positive = darker than the axis) at the reference points.
        /// </summary>
        internal readonly double CornerStops, LongEdgeStops, ShortEdgeStops, HalfDiagonalStops;
        /// <summary>
        /// The corner's radius in the model's units; above 1 with lensfun means extrapolation.
        /// </summary>
        internal readonly double CornerRadius;
        /// <summary>
        /// Your corner against the calibration sensor's corner: above 1 = extrapolation, above 1.1
        /// = a large one.
        /// </summary>
        internal double Extrapolation => CornerRadius / Profile.Model.CalibrationCornerRadius();
        internal const double SmallExtrapolation = 1.1;

        /// <summary>A built flat with its measured depths.</summary>
        /// <param name="flat">The raster, h x w, 1.0 on the axis.</param>
        /// <param name="profile">The profile it was built from.</param>
        /// <param name="cameraCrop">The camera's crop factor used; 0 for none (Adobe only).</param>
        /// <param name="w">The width, pixels.</param>
        /// <param name="h">The height, pixels.</param>
        /// <param name="corner">The depth at the corner box's centre, stops.</param>
        /// <param name="longEdge">The depth at the middle of the long edge, stops.</param>
        /// <param name="shortEdge">The depth at the middle of the short edge, stops.</param>
        /// <param name="halfDiag">The depth at half the half-diagonal, stops.</param>
        /// <param name="cornerRadius">The frame corner's radius, in the model's units.</param>
        internal SyntheticFlatResult(Img flat, LensProfile profile, double cameraCrop, int w, int h,
                                     double corner, double longEdge, double shortEdge, double halfDiag, double cornerRadius)
        {
            Flat = flat;
            Profile = profile;
            CameraCrop = cameraCrop;
            Width = w;
            Height = h;
            CornerStops = corner;
            LongEdgeStops = longEdge;
            ShortEdgeStops = shortEdge;
            HalfDiagonalStops = halfDiag;
            CornerRadius = cornerRadius;
        }
    }

    /// <summary>
    /// Builds the radial flat of a lens profile (<c>--synthetic</c>, and the sixth structure
    /// test through <see cref="VignettingComparer"/>).
    /// </summary>
    internal static class SyntheticFlatBuilder
    {
        /// <summary>
        /// The fraction of the half-side at which the centre of the 5 % corner box sits
        /// (CornerStats).
        /// </summary>
        internal const double CornerBoxFraction = 0.95;

        /// <summary>
        /// Evaluates the profile's model at every pixel centre of a w x h frame and measures its
        /// depth at the reference points. Throws an ArgumentException for a frame under 2 x 2,
        /// and the model's own for a lensfun profile without a crop.
        /// </summary>
        /// <param name="profile">The lens profile to draw.</param>
        /// <param name="w">The frame's width, pixels; at least 2.</param>
        /// <param name="h">The frame's height, pixels; at least 2.</param>
        /// <param name="cameraCrop">The camera's crop factor; 0 for none (Adobe only).</param>
        /// <returns>The flat and its depths; never null.</returns>
        internal static SyntheticFlatResult Build(LensProfile profile, int w, int h, double cameraCrop)
        {
            if (w < 2 || h < 2) throw new ArgumentException(T("the reference frame is too small", "cadrul de referinta e prea mic"));
            VignettingModel m = profile.Model;
            double dmax = Math.Max(w, h);
            var flat = new Img(h, w);
            for (int y = 0; y < h; y++)
            {
                double yN = (y + 0.5 - h / 2.0) / dmax;
                for (int x = 0; x < w; x++)
                {
                    double xN = (x + 0.5 - w / 2.0) / dmax;
                    flat.Set(y, x, m.Factor(xN, yN, cameraCrop));
                }
            }
            double halfW = w / 2.0 / dmax, halfH = h / 2.0 / dmax;
            double axis = m.Factor(0, 0, cameraCrop);
            double Stops(double xN, double yN) => Math.Log2(axis / m.Factor(xN, yN, cameraCrop));
            double f = CornerBoxFraction;
            return new SyntheticFlatResult(flat, profile, cameraCrop, w, h,
                Stops(f * halfW, f * halfH),
                Stops(f * halfW, 0),
                Stops(0, f * halfH),
                Stops(0.5 * halfW, 0.5 * halfH),
                m.Radius(halfW, halfH, cameraCrop));
        }
    }
}
