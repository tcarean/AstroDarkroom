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

// PrintSizeAnalyzer.cs - masuratoarea D3: tabelul de marimi.
//
// Pentru fiecare latime candidata: dpi, pasul pixelului pe hartie, cat de mare iese o
// stea (FWHM x pas), si de la ce distanta ochiul o vede ca punct - acuitatea e ~0.29 mm
// la 1 m si scaleaza liniar. Randul recomandat e cel mai mare la care steaua e punct de
// la distanta de privire: cea data de om, sau, fara ea, 1.5 x diagonala imaginii - de
// unde se priveste un tablou in mod natural.
//
// DISTANTA NATURALA NU ALEGE O MARIME. Si distanta de la care steaua e punct, si cea
// naturala cresc proportional cu latimea; raportul lor e o constanta a imaginii (vezi
// Thresholds.NaturalDistanceTolerance). Deci fara distanta data testul spune doar "e
// destul de clara pentru distanta ei naturala" - la toate marimile deopotriva - si
// recomandarea e cea mai mare care nu e supradimensionata. Cu o distanta data (hol,
// canapea), testul chiar desparte marimile, si acolo se sprijina recomandarea.
//
// Nu exista "dpi minim" aici. Rezolutia reala e FWHM-ul; dpi-ul e doar cati pixeli
// incap intr-un inch, si peste 1/FWHM din el nu mai aduce nimic. Singura data cand
// dpi-ul decide e fara stele - atunci cade pe un prag conservator.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed record SizeRow
    {
        internal double WidthCm { get; init; }
        internal double HeightCm { get; init; }
        internal double Dpi { get; init; }
        internal double PitchMm { get; init; }

        /// <summary>NaN fara FWHM.</summary>
        internal double StarMm { get; init; }

        /// <summary>De la ce distanta (m) steaua e punct. NaN fara FWHM.</summary>
        internal double MinDistanceM { get; init; }

        /// <summary>1.5 x diagonala imaginii printate, in metri.</summary>
        internal double NaturalDistanceM { get; init; }

        /// <summary>Distanta la care s-a judecat randul: cea din parametri, sau cea naturala.</summary>
        internal double ViewingDistanceM { get; init; }

        internal bool PointAtViewingDistance { get; init; }
        internal double OuterWidthCm { get; init; }
        internal double OuterHeightCm { get; init; }
        internal bool Oversize { get; init; }
        internal bool Recommended { get; init; }
    }

    internal sealed record PrintSizeResult
    {
        internal int PixelW { get; init; }
        internal int PixelH { get; init; }
        internal double FwhmPx { get; init; }
        internal bool FwhmKnown => !double.IsNaN(FwhmPx);
        internal double EffectiveDetailPx => FwhmKnown ? PixelW / FwhmPx : double.NaN;
        internal bool DistanceIsExplicit { get; init; }
        internal IReadOnlyList<SizeRow> Rows { get; init; }
        internal SizeRow RecommendedRow => Rows.FirstOrDefault(r => r.Recommended);
    }

    internal static class PrintSizeAnalyzer
    {
        internal static PrintSizeResult Analyze(int pixelW, int pixelH, double fwhmPx, Parameters p)
        {
            bool known = !double.IsNaN(fwhmPx) && fwhmPx > 0;
            var rows = new List<SizeRow>();
            foreach (double wcm in p.CandidateWidthsCm.OrderBy(w => w))
            {
                double hcm = wcm * pixelH / pixelW;
                double dpi = pixelW / (wcm / 2.54);
                double pitch = 25.4 / dpi;
                double star = known ? fwhmPx * pitch : double.NaN;
                double minDist = known ? star / Thresholds.AcuityMmAt1m : double.NaN;
                double natural = Thresholds.ViewingDistanceFactor * Math.Sqrt(wcm * wcm + hcm * hcm) / 100;
                double viewing = p.ViewingDistanceM ?? natural;
                // Toleranta doar pe distanta naturala: cea data de om e a lui, nu se largeste.
                double allowed = p.ViewingDistanceM.HasValue ? viewing : viewing * Thresholds.NaturalDistanceTolerance;
                bool point = known ? minDist <= allowed : dpi >= Thresholds.FallbackMinDpi;
                rows.Add(new SizeRow
                {
                    WidthCm = wcm, HeightCm = hcm, Dpi = dpi, PitchMm = pitch,
                    StarMm = star, MinDistanceM = minDist,
                    NaturalDistanceM = natural, ViewingDistanceM = viewing,
                    PointAtViewingDistance = point,
                    OuterWidthCm = wcm + 2 * (p.PassepartoutCm + p.FrameCm),
                    OuterHeightCm = hcm + 2 * (p.PassepartoutCm + p.FrameCm),
                    Oversize = wcm > Thresholds.OversizeCm,
                });
            }

            // Cea mai mare latime la care steaua e punct si care nu e supradimensionata;
            // daca niciuna nu trece, cea mai mica - un print tot se face.
            SizeRow pick = rows.LastOrDefault(r => r.PointAtViewingDistance && !r.Oversize) ?? rows.FirstOrDefault();
            var final = rows.Select(r => r with { Recommended = ReferenceEquals(r, pick) }).ToList();

            return new PrintSizeResult
            {
                PixelW = pixelW, PixelH = pixelH,
                FwhmPx = known ? fwhmPx : double.NaN,
                DistanceIsExplicit = p.ViewingDistanceM.HasValue,
                Rows = final,
            };
        }
    }
}
