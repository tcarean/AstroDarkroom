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

// CropAnalyzer.cs - the measurement A3: what is cut so that the image fits a given aspect ratio.
// A computation, not a measurement - but it is in Analysis because the report and the window
// consume it like any other result, and because the centred crop is EXACTLY the one the
// acceptance figures were measured on (5554 x 3888 out of 5938 x 3888).

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What a target aspect ratio cuts from the image (A3): the kept, centred rectangle and what
    /// is cut.
    /// </summary>
    internal sealed record CropResult
    {
        internal bool Needed { get; init; }
        internal double NativeAspect { get; init; }
        internal double? TargetAspect { get; init; }

        /// <summary>The kept rectangle, centred. Without a target it is the whole image.</summary>
        internal int X0 { get; init; }
        internal int Y0 { get; init; }
        internal int W { get; init; }
        internal int H { get; init; }

        /// <summary>
        /// "latime" or "inaltime": the axis that is cut. Null when nothing is cut.
        /// </summary>
        internal string Axis { get; init; }
        internal int CutPerSidePx { get; init; }
        internal double CutPct { get; init; }
    }

    /// <summary>Computes the centred crop to a target aspect ratio (A3).</summary>
    internal static class CropAnalyzer
    {
        /// <summary>
        /// The centred crop of a <paramref name="w"/> x <paramref name="h"/> image to
        /// <paramref name="targetAspect"/>: the width is cut when the target is narrower, the
        /// height when it is wider; nothing when there is no target or it is the native one
        /// (within 1e-6).
        /// </summary>
        /// <param name="w">The image's width, in pixels.</param>
        /// <param name="h">The image's height, in pixels.</param>
        /// <param name="targetAspect">The target width / height, positive; null for none.</param>
        /// <returns>
        /// The kept rectangle, the axis cut, the pixels cut on the left or top side and the share
        /// of the axis cut, in percent.
        /// </returns>
        internal static CropResult Analyze(int w, int h, double? targetAspect)
        {
            double native = w / (double)h;
            if (targetAspect == null || Math.Abs(targetAspect.Value - native) < 1e-6)
                return new CropResult { Needed = false, NativeAspect = native, TargetAspect = targetAspect, X0 = 0, Y0 = 0, W = w, H = h };

            double t = targetAspect.Value;
            if (t < native)
            {
                // The target is narrower: the width is cut, equally on both sides.
                int newW = (int)Math.Round(h * t);
                int cut = (w - newW) / 2;
                return new CropResult
                {
                    Needed = true,
                    NativeAspect = native,
                    TargetAspect = t,
                    X0 = cut,
                    Y0 = 0,
                    W = newW,
                    H = h,
                    Axis = "latime",
                    CutPerSidePx = cut,
                    CutPct = 100.0 * (w - newW) / w,
                };
            }
            else
            {
                int newH = (int)Math.Round(w / t);
                int cut = (h - newH) / 2;
                return new CropResult
                {
                    Needed = true,
                    NativeAspect = native,
                    TargetAspect = t,
                    X0 = 0,
                    Y0 = cut,
                    W = w,
                    H = newH,
                    Axis = "inaltime",
                    CutPerSidePx = cut,
                    CutPct = 100.0 * (h - newH) / h,
                };
            }
        }
    }
}
