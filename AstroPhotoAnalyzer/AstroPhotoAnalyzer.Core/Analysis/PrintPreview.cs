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

// PrintPreview.cs - imaginea unui rand al simularii, pentru fereastra de preview (R-045):
// o copie redusa, ridicata cu gamma randului, si clasa fiecarui pixel al ei fata de negrul
// hartiei si de pragul de separare slaba.
//
// Fereastra doar vopseste: ce e "sub negru" si ce e "sub L* 15" se decide aici, cu acelasi
// L* pe care il numara Anexa A. Nu intra in raport si nu e o masuratoare noua: e acelasi
// calcul, pe o imagine mai mica, pastrat ca sa poata fi vazut.

using System;

namespace AstroPhotoAnalyzer
{
    internal enum PreviewClass : byte
    {
        None = 0,
        UnderLowSeparation = 1,
        UnderPaperBlack = 2,
    }

    internal sealed record PrintPreviewResult
    {
        /// <summary>Imaginea redusa si ridicata.</summary>
        internal Raster Image { get; init; }

        /// <summary>Clasa fiecarui pixel al imaginii reduse, in ordinea randurilor.</summary>
        internal PreviewClass[] Classes { get; init; }

        internal double Gamma { get; init; }
        internal double PaperBlackLstar { get; init; }
        internal double PctUnderPaperBlack { get; init; }
        internal double PctUnderLowSeparation { get; init; }
    }

    internal static class PrintPreview
    {
        /// <summary>
        /// Pasul intreg care aduce latura mare a imaginii sub PreviewMaxSide: acelasi pentru
        /// preview si pentru harta, ca dalele hartii sa cada pe pixelii imaginii reduse.
        /// </summary>
        internal static int ReductionStride(Raster img)
            => Math.Max(1, (int)Math.Ceiling(Math.Max(img.W, img.H) / (double)Thresholds.PreviewMaxSide));

        /// <summary>
        /// Imaginea <paramref name="img"/> redusa sub Thresholds.PreviewMaxSide, ridicata cu
        /// <paramref name="gamma"/> (1 = asa cum e), si clasele pixelilor ei la Dmax-ul dat.
        /// </summary>
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
