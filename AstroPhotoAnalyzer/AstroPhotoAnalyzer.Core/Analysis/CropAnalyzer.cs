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
// acceptance figures were measured on (5554 x 3888 out of 5938 x 3888). A crop exists when it
// removes a pixel, not when the ratio differs: 4961 x 3508 at 297:210 is no crop, and 3508 x 2480
// at the same ratio cuts one pixel, on the right side alone (ADR 0043).

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
        /// "latime" or "inaltime": the axis that is cut. Null when no crop is needed: a crop is
        /// needed only when the target removes at least one pixel (ADR 0043; until the review of
        /// 2026-10-23 a ratio 1e-6 off the native one was a crop of 0 px).
        /// </summary>
        internal string Axis { get; init; }

        /// <summary>
        /// The pixels cut on the first side of the axis (the left, or the top), and on the second
        /// (the right, or the bottom): equal, or the second one more on an odd cut. 0 without a
        /// crop.
        /// </summary>
        internal int CutStartPx { get; init; }
        internal int CutEndPx { get; init; }
        internal double CutPct { get; init; }
    }

    /// <summary>Computes the centred crop to a target aspect ratio (A3).</summary>
    internal static class CropAnalyzer
    {
        /// <summary>
        /// The centred crop of a <paramref name="w"/> x <paramref name="h"/> image to
        /// <paramref name="targetAspect"/>: the width is cut when the target is narrower, the
        /// height when it is wider, to round(h x target) or round(w / target); no crop when
        /// there is no target or the rounded size removes no pixel (ADR 0043).
        /// </summary>
        /// <param name="w">The image's width, in pixels.</param>
        /// <param name="h">The image's height, in pixels.</param>
        /// <param name="targetAspect">The target width / height, positive; null for none.</param>
        /// <returns>
        /// The kept rectangle, the axis cut, the pixels cut on each side and the share of the
        /// axis cut, in percent.
        /// </returns>
        internal static CropResult Analyze(int w, int h, double? targetAspect)
        {
            double native = w / (double)h;
            var none = new CropResult { Needed = false, NativeAspect = native, TargetAspect = targetAspect, X0 = 0, Y0 = 0, W = w, H = h };
            if (targetAspect == null) return none;

            double t = targetAspect.Value;
            if (t < native)
            {
                // The target is narrower: the width is cut, half on each side; with an odd cut the
                // right side loses one pixel more (the bottom one, below).
                // A target narrower than the image rounds to at most w; the floor of 1 px is for
                // an absurd target (1:10000), which must not leave a width of 0.
                int newW = Math.Max(1, (int)Math.Round(h * t));
                if (newW == w) return none;
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
                    CutStartPx = cut,
                    CutEndPx = w - newW - cut,
                    CutPct = 100.0 * (w - newW) / w,
                };
            }
            else
            {
                int newH = Math.Max(1, (int)Math.Round(w / t));
                if (newH == h) return none;
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
                    CutStartPx = cut,
                    CutEndPx = h - newH - cut,
                    CutPct = 100.0 * (h - newH) / h,
                };
            }
        }
    }
}
