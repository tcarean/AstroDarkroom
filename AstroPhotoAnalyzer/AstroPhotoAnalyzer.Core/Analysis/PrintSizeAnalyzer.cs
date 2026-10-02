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
// la 1 m si scaleaza liniar. Testul: steaua e punct de la distanta de privire - cea data
// de om, sau, fara ea, 1.5 x diagonala printului, de unde se priveste un tablou in mod
// natural.
//
// DISTANTA NATURALA NU ALEGE O MARIME. Si distanta de la care steaua e punct, si cea
// naturala cresc proportional cu latimea; raportul lor e o constanta a imaginii (vezi
// Thresholds.NaturalDistanceTolerance). Deci fara distanta data testul spune doar "e
// destul de clara pentru distanta ei naturala" - la toate marimile deopotriva - si NU
// SE RECOMANDA NICIUN RAND: steluta ar fi cazut pe cel mai mare candidat sub limita,
// adica pe --sizes, nu pe imagine. Cu o distanta data (hol, canapea) si cu FWHM masurat
// testul chiar desparte marimile, si doar acolo se sprijina o recomandare. Randul de
// referinta (cel mai mare care trece si nu e supradimensionat, altfel cel mai mic)
// exista mereu: de el vorbesc recomandarile ("pana la X cm") si la dpi-ul lui se
// judeca palierul.
//
// Nu exista "dpi minim" aici. Rezolutia reala e FWHM-ul; dpi-ul e doar cati pixeli
// incap intr-un inch, si peste 1/FWHM din el nu mai aduce nimic. Singura data cand
// dpi-ul decide e fara stele - si atunci nu judeca detaliul, ci doar grila de pixeli:
// invizibila de la distanta data (25.4 / (0.29 x d) dpi) sau, fara ea, de la ~0.5 m
// (Thresholds.FallbackMinDpi). E o limita a fisierului, nu o masuratoare, deci nici
// acolo nu se recomanda un rand.

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

        /// <summary>De la ce distanta (m) dispare grila de pixeli: pasul / acuitate. Nu depinde de stele.</summary>
        internal double PixelDistanceM { get; init; }

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

        /// <summary>Doar cand testul desparte randurile - vezi PrintSizeResult.CanRecommend.</summary>
        internal bool Recommended { get; init; }
    }

    internal sealed record PrintSizeResult
    {
        internal int PixelW { get; init; }
        internal int PixelH { get; init; }
        internal double FwhmPx { get; init; }
        internal bool FwhmKnown => !double.IsNaN(FwhmPx);
        internal double EffectiveDetailPx => FwhmKnown ? PixelW / FwhmPx : double.NaN;

        /// <summary>
        /// Cat de lat ar fi printul cu pasul pixelului unui monitor (Thresholds.ScreenPixelMm):
        /// ce arata o vedere la 100 % pe ecran. Doar pentru Preview.
        /// </summary>
        internal double ScreenEquivalentWidthCm => PixelW * Thresholds.ScreenPixelMm / 10;

        /// <summary>
        /// De cate ori distanta fata de monitor trebuie privita dala de 100 % ca sa arate ca
        /// printul de referinta de la distanta monitorului: latimea echivalenta / latimea lui.
        /// Sub 1 printul e mai mare decat ecranul la 100 %. NaN fara rand de referinta.
        /// </summary>
        internal double ScreenStepBackFactor => ReferenceRow == null ? double.NaN : ScreenEquivalentWidthCm / ReferenceRow.WidthCm;
        internal bool DistanceIsExplicit { get; init; }

        /// <summary>
        /// Adevarat doar cu distanta data SI FWHM masurat - singurul caz in care testul
        /// desparte randurile pe o masuratoare. Fals la distanta naturala (verdictul e
        /// acelasi pe fiecare rand) si fals fara stele (regula de dpi e despre fisier, nu
        /// despre detaliu). Acolo o steluta ar fi o minciuna.
        /// </summary>
        internal bool CanRecommend { get; init; }

        /// <summary>
        /// Pragul de dpi aplicat fara FWHM: cel fizic din distanta data, 25.4 / (0.29 x d),
        /// altfel Thresholds.FallbackMinDpi. NaN cu FWHM.
        /// </summary>
        internal double FallbackDpi { get; init; }

        /// <summary>
        /// Cel mai mare rand care trece si nu e supradimensionat, altfel cel mai mic. Exista
        /// mereu (cu cel putin un candidat): recomandarile vorbesc de el si palierul se
        /// judeca la dpi-ul lui, fie ca e recomandat sau nu.
        /// </summary>
        internal SizeRow ReferenceRow { get; init; }

        /// <summary>Randul de referinta cand exista o recomandare, altfel null.</summary>
        internal SizeRow RecommendedRow => CanRecommend ? ReferenceRow : null;

        /// <summary>
        /// "Punct de la" / distanta naturala - acelasi pe fiecare rand, o constanta a imaginii:
        /// 1 = fix atat de clara cat ii trebuie pentru distanta ei naturala, 10 = ar trebui
        /// privita de zece ori mai de departe. NaN fara FWHM.
        /// </summary>
        internal double NaturalRatio { get; init; }

        internal IReadOnlyList<SizeRow> Rows { get; init; }
    }

    /// <summary>Un rand al scarii dpi - distanta: de la cati metri dispare grila de pixeli la dpi-ul asta.</summary>
    internal sealed record DpiLadderRow(double Dpi, double PitchMm, double DistanceM, string Meaning);

    internal static class PrintSizeAnalyzer
    {
        internal static PrintSizeResult Analyze(int pixelW, int pixelH, double fwhmPx, Parameters p)
        {
            bool known = !double.IsNaN(fwhmPx) && fwhmPx > 0;
            // Fara FWHM se judeca doar grila de pixeli: invizibila de la distanta data sau,
            // fara ea, de la ~0.5 m (conventia de 180 dpi).
            double fallbackDpi = known ? double.NaN
                : p.ViewingDistanceM.HasValue ? 25.4 / (Thresholds.AcuityMmAt1m * p.ViewingDistanceM.Value)
                : Thresholds.FallbackMinDpi;
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
                bool point = known ? minDist <= allowed : dpi >= fallbackDpi;
                rows.Add(new SizeRow
                {
                    WidthCm = wcm,
                    HeightCm = hcm,
                    Dpi = dpi,
                    PitchMm = pitch,
                    PixelDistanceM = pitch / Thresholds.AcuityMmAt1m,
                    StarMm = star,
                    MinDistanceM = minDist,
                    NaturalDistanceM = natural,
                    ViewingDistanceM = viewing,
                    PointAtViewingDistance = point,
                    OuterWidthCm = wcm + 2 * (p.PassepartoutCm + p.FrameCm),
                    OuterHeightCm = hcm + 2 * (p.PassepartoutCm + p.FrameCm),
                    Oversize = wcm > Thresholds.OversizeCm,
                });
            }

            // Randul de referinta: cea mai mare latime la care steaua e punct si care nu e
            // supradimensionata; daca niciuna nu trece, cea mai mica - un print tot se face.
            // E recomandat doar cand testul a avut cum sa-l aleaga pe o masuratoare.
            bool canRecommend = p.ViewingDistanceM.HasValue && known;
            int pick = rows.FindLastIndex(r => r.PointAtViewingDistance && !r.Oversize);
            if (pick < 0 && rows.Count > 0) pick = 0;
            var final = rows.Select((r, i) => r with { Recommended = canRecommend && i == pick }).ToList();

            return new PrintSizeResult
            {
                PixelW = pixelW,
                PixelH = pixelH,
                FwhmPx = known ? fwhmPx : double.NaN,
                DistanceIsExplicit = p.ViewingDistanceM.HasValue,
                CanRecommend = canRecommend,
                FallbackDpi = fallbackDpi,
                ReferenceRow = pick >= 0 ? final[pick] : null,
                NaturalRatio = known && rows.Count > 0 ? rows[0].MinDistanceM / rows[0].NaturalDistanceM : double.NaN,
                Rows = final,
            };
        }

        /// <summary>
        /// Scara de referinta, ca sa se citeasca coloana "dpi" fara mitul celor 300: pasul
        /// pixelului e 25.4 / dpi si grila dispare de la pas / acuitate. Nu masoara nimic din
        /// imagine - e fereastra care o arata. Reperele: 300 = citit, 240 = nativul multor
        /// imprimante, 180 = conventia fara stele, 100 = poster, 60 = de pe canapea. Cu
        /// distanta data, ultimul rand e pragul ei.
        /// </summary>
        internal static IReadOnlyList<DpiLadderRow> DpiLadder(double? viewingDistanceM)
        {
            var rows = new List<DpiLadderRow>
            {
                At(Thresholds.ReferenceDpi, "citit, tinut in mana - de aici mitul"),
                At(240, "nativul multor imprimante"),
                At(Thresholds.FallbackMinDpi, "lungimea bratului, cand te apropii - conventia fara stele"),
                At(100, "poster"),
                At(60, "de pe canapea"),
            };
            if (viewingDistanceM.HasValue)
                rows.Add(At(25.4 / (Thresholds.AcuityMmAt1m * viewingDistanceM.Value), "distanta data"));
            return rows;

            static DpiLadderRow At(double dpi, string meaning)
            {
                double pitch = 25.4 / dpi;
                return new DpiLadderRow(dpi, pitch, pitch / Thresholds.AcuityMmAt1m, meaning);
            }
        }
    }
}
