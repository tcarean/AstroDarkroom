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

// PrintSizeAnalyzer.cs - the measurement D3: the size table.
//
// For each candidate width: the dpi, the pixel's pitch on paper, how large a star comes out
// (FWHM x pitch), and from what distance the eye sees it as a point - the acuity is ~0.29 mm at
// 1 m and scales linearly. The test: the star is a point from the viewing distance - the one the
// user gave, or, without it, 1.5 x the print's diagonal, from where a picture is naturally
// looked at.
//
// THE NATURAL DISTANCE DOES NOT PICK A SIZE. Both the distance from which the star is a point and
// the natural one grow in proportion to the width; their ratio is a constant of the image (see
// Thresholds.NaturalDistanceTolerance). So without a given distance the test says only "it is
// sharp enough for its natural distance" - at every size alike - and NO ROW IS RECOMMENDED: the
// asterisk would have landed on the largest candidate under the limit, that is on --sizes, not on
// the image. With a given distance (a hallway, a sofa) and a measured FWHM the test really
// separates the sizes, and only there does a recommendation rest on it. The reference row (the
// largest that passes and is not oversize, otherwise the smallest) always exists: the
// recommendations speak of it ("pana la X cm") and the plateau is judged at its dpi.
//
// There is no "minimum dpi" here. The real resolution is the FWHM; the dpi is only how many
// pixels fit in an inch, and beyond 1/FWHM of it nothing more is gained. The only time the dpi
// decides is without stars - and then it does not judge the detail, only the pixel grid:
// invisible from the given distance (25.4 / (0.29 x d) dpi) or, without one, from ~0.5 m
// (Thresholds.FallbackMinDpi). It is a limit of the file, not a measurement, so there too no row
// is recommended.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>A row of the size table (D3): a candidate width and what it gives.</summary>
    internal sealed record SizeRow
    {
        internal double WidthCm { get; init; }
        internal double HeightCm { get; init; }
        internal double Dpi { get; init; }
        internal double PitchMm { get; init; }

        /// <summary>
        /// From what distance (m) the pixel grid vanishes: the pitch / the acuity. It does not
        /// depend on the stars.
        /// </summary>
        internal double PixelDistanceM { get; init; }

        /// <summary>NaN without FWHM.</summary>
        internal double StarMm { get; init; }

        /// <summary>From what distance (m) the star is a point. NaN without FWHM.</summary>
        internal double MinDistanceM { get; init; }

        /// <summary>1.5 x the printed image's diagonal, in metres.</summary>
        internal double NaturalDistanceM { get; init; }

        /// <summary>
        /// The distance the row was judged at: the one in the parameters, or the natural one.
        /// </summary>
        internal double ViewingDistanceM { get; init; }

        internal bool PointAtViewingDistance { get; init; }
        internal double OuterWidthCm { get; init; }
        internal double OuterHeightCm { get; init; }
        internal bool Oversize { get; init; }

        /// <summary>
        /// Only when the test separates the rows - see PrintSizeResult.CanRecommend.
        /// </summary>
        internal bool Recommended { get; init; }
    }

    /// <summary>
    /// The size table (D3): the rows of the candidate widths, the reference row, and whether a
    /// row can be recommended.
    /// </summary>
    internal sealed record PrintSizeResult
    {
        internal int PixelW { get; init; }
        internal int PixelH { get; init; }
        internal double FwhmPx { get; init; }
        internal bool FwhmKnown => !double.IsNaN(FwhmPx);
        internal double EffectiveDetailPx => FwhmKnown ? PixelW / FwhmPx : double.NaN;

        /// <summary>
        /// How wide the print would be at a monitor's pixel pitch (Thresholds.ScreenPixelMm):
        /// what a 100 % view on screen shows. Only for the Preview.
        /// </summary>
        internal double ScreenEquivalentWidthCm => PixelW * Thresholds.ScreenPixelMm / 10;

        /// <summary>
        /// At how many times the monitor's distance the 100 % tile must be viewed to look like
        /// the reference print from the monitor's distance: the equivalent width / its width.
        /// Under 1 the print is larger than the screen at 100 %. NaN without a reference row.
        /// </summary>
        internal double ScreenStepBackFactor => ReferenceRow == null ? double.NaN : ScreenEquivalentWidthCm / ReferenceRow.WidthCm;
        internal bool DistanceIsExplicit { get; init; }

        /// <summary>
        /// True only with a given distance AND a measured FWHM - the only case in which the test
        /// separates the rows on a measurement. False at the natural distance (the verdict is the
        /// same on every row) and false without stars (the dpi rule is about the file, not about
        /// the detail). There an asterisk would be a lie.
        /// </summary>
        internal bool CanRecommend { get; init; }

        /// <summary>
        /// The dpi threshold applied without FWHM: the physical one from the given distance,
        /// 25.4 / (0.29 x d), otherwise Thresholds.FallbackMinDpi. NaN with FWHM.
        /// </summary>
        internal double FallbackDpi { get; init; }

        /// <summary>
        /// The largest row that passes and is not oversize, otherwise the smallest. It always
        /// exists (with at least one candidate): the recommendations speak of it and the plateau
        /// is judged at its dpi, whether it is recommended or not.
        /// </summary>
        internal SizeRow ReferenceRow { get; init; }

        /// <summary>The reference row when there is a recommendation, otherwise null.</summary>
        internal SizeRow RecommendedRow => CanRecommend ? ReferenceRow : null;

        /// <summary>
        /// "Punct de la" / the natural distance - the same on every row, a constant of the image:
        /// 1 = exactly as sharp as it needs to be for its natural distance, 10 = it would have to
        /// be viewed from ten times farther. NaN without FWHM.
        /// </summary>
        internal double NaturalRatio { get; init; }

        internal IReadOnlyList<SizeRow> Rows { get; init; }
    }

    /// <summary>
    /// A row of the dpi - distance ladder: from how many metres the pixel grid vanishes at this
    /// dpi.
    /// </summary>
    /// <param name="Dpi">The dots per inch.</param>
    /// <param name="PitchMm">The pixel's pitch on paper, 25.4 / dpi, in mm.</param>
    /// <param name="DistanceM">
    /// The distance from which the grid vanishes, the pitch / the acuity, in metres.
    /// </param>
    /// <param name="Meaning">
    /// What the row is, in the interface language as the window shows it ("poster", "de pe canapea").
    /// </param>
    internal sealed record DpiLadderRow(double Dpi, double PitchMm, double DistanceM, string Meaning);

    /// <summary>Builds the size table (D3) and the dpi - distance ladder.</summary>
    internal static class PrintSizeAnalyzer
    {
        /// <summary>
        /// The size table of a <paramref name="pixelW"/> x <paramref name="pixelH"/> print: one
        /// row per candidate width, judged on the star's size at the viewing distance or, without
        /// a FWHM, on the pixel grid.
        /// </summary>
        /// <param name="pixelW">The width that prints, in pixels: the crop's.</param>
        /// <param name="pixelH">The height that prints, in pixels.</param>
        /// <param name="fwhmPx">
        /// The median FWHM, in pixels; NaN, 0 or less when there is none: StarAnalyzer gives NaN
        /// under <see cref="Thresholds.StarMinCount"/> stars, so 1 to 19 stars count as none.
        /// </param>
        /// <param name="p">
        /// The run's parameters: the candidate widths, the viewing distance, the passepartout and
        /// the frame.
        /// </param>
        /// <returns>
        /// The rows by increasing width, the reference row, and the recommendation when the rows
        /// separate on a measurement.
        /// </returns>
        internal static PrintSizeResult Analyze(int pixelW, int pixelH, double fwhmPx, Parameters p)
        {
            bool known = !double.IsNaN(fwhmPx) && fwhmPx > 0;
            // Without FWHM only the pixel grid is judged: invisible from the given distance or,
            // without one, from ~0.5 m (the 180 dpi convention).
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
                // The tolerance only on the natural distance: the one the user gave is theirs, it
                // is not widened.
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

            // The reference row: the largest width at which the star is a point and which is not
            // oversize; if none passes, the smallest - a print is made anyway. It is recommended
            // only when the test had a way to choose it on a measurement.
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
        /// The reference ladder, so that the "dpi" column is read without the myth of the 300:
        /// the pixel's pitch is 25.4 / dpi and the grid vanishes from pitch / acuity. It measures
        /// nothing of the image: the window shows it. The marks: 300 = reading, 240 = the native
        /// resolution of many printers, 180 = the convention without stars, 100 = poster, 60 =
        /// from the sofa. With a given distance, the last row is its threshold.
        /// </summary>
        /// <param name="viewingDistanceM">
        /// The given viewing distance, in metres; null for none.
        /// </param>
        /// <returns>
        /// The rows, <see cref="Thresholds.ReferenceDpi"/> first; with a given distance, its own
        /// row last.
        /// </returns>
        internal static IReadOnlyList<DpiLadderRow> DpiLadder(double? viewingDistanceM)
        {
            var rows = new List<DpiLadderRow>
            {
                At(Thresholds.ReferenceDpi, Tr.Pick("reading, held in the hand - where the myth comes from", "citit, tinut in mana - de aici mitul")),
                At(240, Tr.Pick("native to many printers", "nativul multor imprimante")),
                At(Thresholds.FallbackMinDpi, Tr.Pick("arm's length, when you step closer - the convention without stars", "lungimea bratului, cand te apropii - conventia fara stele")),
                At(100, "poster"),
                At(60, Tr.Pick("from the sofa", "de pe canapea")),
            };
            if (viewingDistanceM.HasValue)
                rows.Add(At(25.4 / (Thresholds.AcuityMmAt1m * viewingDistanceM.Value), Tr.Pick("the given distance", "distanta data")));
            return rows;

            static DpiLadderRow At(double dpi, string meaning)
            {
                double pitch = 25.4 / dpi;
                return new DpiLadderRow(dpi, pitch, pitch / Thresholds.AcuityMmAt1m, meaning);
            }
        }
    }
}
