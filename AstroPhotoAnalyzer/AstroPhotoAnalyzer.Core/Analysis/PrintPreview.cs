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

// PrintPreview.cs - the image of a simulation row, for the preview window (R-045): a reduced
// copy, lifted with the row's gamma, and the class of each of its pixels against paper black
// and the low-separation threshold.
//
// The window only paints: what is "sub negru" and what is "sub L* 15" is decided here, with
// the same L* that Anexa A counts. It is not in the report and is not a new measurement: it is
// the same computation, on a smaller image, kept so that it can be seen.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The class of a preview pixel: under paper black, under the low-separation L* and not under
    /// paper black, or neither.
    /// </summary>
    internal enum PreviewClass : byte
    {
        None = 0,
        UnderLowSeparation = 1,
        UnderPaperBlack = 2,
    }

    /// <summary>
    /// A simulation row as an image: the reduced, lifted copy, the class of each of its pixels and
    /// the two shares.
    /// </summary>
    internal sealed record PrintPreviewResult
    {
        /// <summary>The reduced and lifted image.</summary>
        internal Raster Image { get; init; }

        /// <summary>The class of each pixel of the reduced image, in row order.</summary>
        internal PreviewClass[] Classes { get; init; }

        internal double Gamma { get; init; }
        internal double PaperBlackLstar { get; init; }
        internal double PctUnderPaperBlack { get; init; }
        internal double PctUnderLowSeparation { get; init; }
    }

    /// <summary>A simulation row as an image, for the preview window (R-045).</summary>
    internal static class PrintPreview
    {
        /// <summary>
        /// The integer step that brings the image's long side to at most PreviewMaxSide: the same
        /// for the preview and for the map, so that the map's patches fall on the pixels of the
        /// reduced image.
        /// </summary>
        /// <param name="img">The image; only its size is read.</param>
        /// <returns>The step, 1 or more.</returns>
        internal static int ReductionStride(Raster img)
            => Math.Max(1, (int)Math.Ceiling(Math.Max(img.W, img.H) / (double)Thresholds.PreviewMaxSide));

        /// <summary>
        /// The image <paramref name="img"/> reduced to at most Thresholds.PreviewMaxSide, lifted
        /// with <paramref name="gamma"/> (1 = as it is), and the classes of its pixels at the
        /// given Dmax.
        /// </summary>
        /// <param name="img">The image, 0..1.</param>
        /// <param name="gamma">
        /// The row's gamma, positive, as <see cref="RasterOps.GammaFor"/> gives it; within 1e-12
        /// of 1 the reduced image is left as it is.
        /// </param>
        /// <param name="dmax">The paper's Dmax, positive.</param>
        /// <returns>
        /// The reduced, lifted image (<paramref name="img"/> itself when it needs neither), the
        /// class of each of its pixels, and the percentages of its pixels under paper black and
        /// under the low-separation L* (those under paper black included).
        /// </returns>
        internal static PrintPreviewResult Render(Raster img, double gamma, double dmax)
        {
            Raster small = RasterOps.Subsample(img, ReductionStride(img));
            Raster lifted = Math.Abs(gamma - 1) < 1e-12 ? small : RasterOps.Gamma(small, gamma);
            float[] L = RasterOps.LstarPlane(lifted, 1);
            double black = ColorMath.PaperBlackLstar(dmax);
            var classes = new PreviewClass[L.Length];
            long nBlack = 0, nLow = 0;
            for (int i = 0; i < L.Length; i++)
            {
                if (L[i] < black) { classes[i] = PreviewClass.UnderPaperBlack; nBlack++; nLow++; }
                else if (L[i] < Thresholds.LowSeparationLstar) { classes[i] = PreviewClass.UnderLowSeparation; nLow++; }
            }
            return new PrintPreviewResult
            {
                Image = lifted,
                Classes = classes,
                Gamma = gamma,
                PaperBlackLstar = black,
                PctUnderPaperBlack = 100.0 * nBlack / L.Length,
                PctUnderLowSeparation = 100.0 * nLow / L.Length,
            };
        }
    }
}
