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
// and the low-separation threshold, taken from the 32 px patch the pixel lies in (ADR 0082).
//
// The window only paints: what is "sub negru" and what is "sub L* 15" is decided here, on the
// patches' L* at the row's lift, the very figures the simulation table counts for the row
// (R-043, ADR 0035). It is not in the report and is not a new measurement: it is the same
// computation, kept so that it can be seen. A pixel is not classed on its own: a stray dark
// pixel in a patch over black does not show on paper, and painted it read as a zone.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The class of a preview pixel, its patch's: under paper black, under the low-separation
    /// L* and not under paper black, or neither.
    /// </summary>
    internal enum PreviewClass : byte
    {
        None = 0,
        UnderLowSeparation = 1,
        UnderPaperBlack = 2,
    }

    /// <summary>
    /// A simulation row as an image: the reduced, lifted copy, the class of each of its pixels
    /// (its patch's) and the two shares of the patches.
    /// </summary>
    internal sealed record PrintPreviewResult
    {
        /// <summary>The reduced and lifted image.</summary>
        internal Raster Image { get; init; }

        /// <summary>
        /// The class of each pixel of the reduced image, in row order: that of the 32 px patch
        /// the pixel lies in; None past the last whole patch, at the right and the bottom.
        /// </summary>
        internal PreviewClass[] Classes { get; init; }

        internal double Gamma { get; init; }
        internal double PaperBlackLstar { get; init; }

        /// <summary>The percentage of the patches under paper black: the row's "negru pur".</summary>
        internal double PctUnderPaperBlack { get; init; }

        /// <summary>
        /// The percentage of the patches under the low-separation L*, those under paper black
        /// included (a share of the patches, unlike the row's pixel share under L* 15).
        /// </summary>
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
        /// given Dmax, each pixel's the class of the 32 px patch of the simulation's grid it lies
        /// in (ADR 0082).
        /// </summary>
        /// <param name="img">The image, 0..1.</param>
        /// <param name="gamma">
        /// The row's gamma, positive, as <see cref="RasterOps.GammaFor"/> gives it; within 1e-12
        /// of 1 the reduced image is left as it is.
        /// </param>
        /// <param name="dmax">The paper's Dmax, positive.</param>
        /// <param name="asIs">
        /// Whether the row is the simulation's "as is" row: its patches are then measured exactly,
        /// as the simulation measures that row, and every other row's through the lift table of
        /// its gamma, even a gamma of 1 (a grid target equal to the background), so that the
        /// shares are the row's to the last decimal.
        /// </param>
        /// <returns>
        /// The reduced, lifted image (<paramref name="img"/> itself when it needs neither), the
        /// class of each of its pixels (its patch's; None past the last whole patch), and the
        /// percentages of the patches under paper black and under the low-separation L* (those
        /// under paper black included).
        /// </returns>
        internal static PrintPreviewResult Render(Raster img, double gamma, double dmax, bool asIs)
        {
            int stride = ReductionStride(img);
            Raster small = RasterOps.Subsample(img, stride);
            Raster lifted = Math.Abs(gamma - 1) < 1e-12 ? small : RasterOps.Gamma(small, gamma);

            // The patches' L* at this lift: the simulation's own computation for the row, on the
            // same subsample and grid (R-044, ADR 0035), exact on the "as is" row as there. The
            // subsample is the preview's own when the two steps coincide.
            Raster sub = stride == Thresholds.TonalStride ? small : RasterOps.Subsample(img, Thresholds.TonalStride);
            float[] patchL = asIs ? DustAnalyzer.PatchLstars(sub) : DustAnalyzer.PatchLstars(sub, new LiftTable(gamma));
            var (cols, rows, side) = DustAnalyzer.Grid(img.W, img.H, Thresholds.TonalStride);
            double black = ColorMath.PaperBlackLstar(dmax);
            var patchClass = new PreviewClass[patchL.Length];
            long nBlack = 0, nLow = 0;
            for (int i = 0; i < patchL.Length; i++)
            {
                if (patchL[i] < black) { patchClass[i] = PreviewClass.UnderPaperBlack; nBlack++; nLow++; }
                else if (patchL[i] < Thresholds.LowSeparationLstar) { patchClass[i] = PreviewClass.UnderLowSeparation; nLow++; }
            }

            // Each reduced pixel takes its patch's class, by the map's geometry (a patch is
            // side x TonalStride image pixels); past the last whole patch there is none.
            int w = lifted.W, h = lifted.H;
            double patchPx = side * (double)Thresholds.TonalStride;
            var classes = new PreviewClass[w * h];
            for (int y = 0; y < h; y++)
            {
                int by = (int)(y * stride / patchPx);
                if (by >= rows) break;
                for (int x = 0; x < w; x++)
                {
                    int bx = (int)(x * stride / patchPx);
                    if (bx >= cols) break;
                    classes[y * w + x] = patchClass[by * cols + bx];
                }
            }
            // The grid has at least one patch for any raster of a pixel or more.
            return new PrintPreviewResult
            {
                Image = lifted,
                Classes = classes,
                Gamma = gamma,
                PaperBlackLstar = black,
                PctUnderPaperBlack = 100.0 * nBlack / patchL.Length,
                PctUnderLowSeparation = 100.0 * nLow / patchL.Length,
            };
        }
    }
}
