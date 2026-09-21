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

namespace AstroPhotoAnalyzer.Tests;

public class PrintSizeAnalyzerTests
{
    const int W = 5938, H = 3888;
    const double Fwhm = 3.0;

    static SizeRow Row(PrintSizeResult r, double width) => r.Rows.Single(x => x.WidthCm == width);

    /// <summary>Tabelul din conversatie: 70 cm -> 215 dpi, stea 0.35 mm, punct de la 1.2 m, rama ~90 x 66.</summary>
    [Fact]
    public void TheSeventyCentimetreRowMatchesTheConversation()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, Fwhm, new Parameters());
        var row = Row(r, 70);

        Assert.InRange(row.Dpi, 215.3, 215.7);
        Assert.InRange(row.HeightCm, 45.8, 45.9);
        Assert.InRange(row.PitchMm, 0.117, 0.119);
        Assert.InRange(row.StarMm, 0.35, 0.36);
        Assert.InRange(row.MinDistanceM, 1.21, 1.23);
        Assert.InRange(row.NaturalDistanceM, 1.25, 1.26);
        Assert.Equal(90, row.OuterWidthCm, 6);
        Assert.Equal(65.83, row.OuterHeightCm, 1);
        Assert.False(row.Oversize);
        Assert.True(row.PointAtViewingDistance);
        Assert.Equal(1979, r.EffectiveDetailPx, 0);
    }

    [Fact]
    public void AtTheNaturalDistanceEveryRowUpToAHundredPasses()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, Fwhm, new Parameters());

        Assert.False(r.DistanceIsExplicit);
        Assert.All(r.Rows, row => Assert.True(row.PointAtViewingDistance));
        Assert.False(r.CanRecommend);
        Assert.Null(r.RecommendedRow);
        Assert.DoesNotContain(r.Rows, row => row.Recommended);
        Assert.Equal(100, r.ReferenceRow.WidthCm);
    }

    /// <summary>
    /// La distanta naturala raportul "punct de la" / naturala e acelasi pe fiecare rand,
    /// deci o steluta ar fi picat pe --sizes, nu pe imagine: nu se recomanda nimic. Cu
    /// distanta data si FWHM masurat randurile se despart pe o masuratoare si steluta
    /// exista. Fara stele regula de dpi e despre fisier, nu despre detaliu: nici acolo.
    /// </summary>
    [Fact]
    public void OnlyATestThatSeparatesTheRowsRecommendsOne()
    {
        var natural = PrintSizeAnalyzer.Analyze(W, H, 3.13, new Parameters());
        Assert.False(natural.CanRecommend);
        Assert.InRange(natural.NaturalRatio, 1.00, 1.03);
        Assert.All(natural.Rows, row => Assert.Equal(natural.NaturalRatio, row.MinDistanceM / row.NaturalDistanceM, 6));

        Assert.True(PrintSizeAnalyzer.Analyze(W, H, 3.13, new Parameters { ViewingDistanceM = 1.5 }).CanRecommend);

        var noStars = PrintSizeAnalyzer.Analyze(W, H, double.NaN, new Parameters { ViewingDistanceM = 1.5 });
        Assert.False(noStars.CanRecommend);
        Assert.True(double.IsNaN(noStars.NaturalRatio));
    }

    /// <summary>
    /// La distanta naturala raportul punct/natural e o constanta a imaginii: pe cea de
    /// referinta 1.018 cu FWHM 3.13. Fara toleranta toate randurile picau cu 2 % si
    /// randul de referinta cadea pe 50; cu ea, toate trec la fel si referinta e 100. O
    /// imagine chiar moale (FWHM 4) pica la toate, tot deopotriva. Recomandat nu e niciunul.
    /// </summary>
    [Fact]
    public void TheNaturalDistanceVerdictIsTheSameAtEverySizeAndHasATolerance()
    {
        var real = PrintSizeAnalyzer.Analyze(W, H, 3.13, new Parameters());
        Assert.All(real.Rows, row => Assert.True(row.MinDistanceM > row.NaturalDistanceM));   // ar pica strict
        Assert.All(real.Rows, row => Assert.True(row.PointAtViewingDistance));                // trece cu toleranta
        Assert.Equal(100, real.ReferenceRow.WidthCm);
        Assert.Null(real.RecommendedRow);

        var soft = PrintSizeAnalyzer.Analyze(W, H, 4.0, new Parameters());
        Assert.All(soft.Rows, row => Assert.False(row.PointAtViewingDistance));
        Assert.Equal(50, soft.ReferenceRow.WidthCm);
        Assert.Null(soft.RecommendedRow);

        // Cu distanta data nu exista toleranta: e a omului.
        var given = PrintSizeAnalyzer.Analyze(W, H, 3.13, new Parameters { ViewingDistanceM = 1.3 });
        Assert.False(Row(given, 80).PointAtViewingDistance);   // 1.45 m > 1.3
        Assert.True(Row(given, 70).PointAtViewingDistance);    // 1.27 m
    }

    [Fact]
    public void AnExplicitDistanceOfOneAndAHalfMetresRecommendsEighty()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, Fwhm, new Parameters { ViewingDistanceM = 1.5 });

        Assert.True(r.DistanceIsExplicit);
        Assert.True(Row(r, 80).PointAtViewingDistance);
        Assert.False(Row(r, 90).PointAtViewingDistance);
        Assert.Equal(80, r.RecommendedRow.WidthCm);
        Assert.Single(r.Rows, row => row.Recommended);
    }

    [Fact]
    public void AnOversizeRowIsNeverTheReferenceEvenIfItPasses()
    {
        var p = new Parameters { CandidateWidthsCm = new double[] { 70, 111 } };
        var r = PrintSizeAnalyzer.Analyze(W, H, Fwhm, p);

        Assert.True(Row(r, 111).Oversize);
        Assert.True(Row(r, 111).PointAtViewingDistance);
        Assert.Equal(70, r.ReferenceRow.WidthCm);
    }

    [Fact]
    public void WithoutStarsTheFallbackIsADpiFloor()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, double.NaN, new Parameters());

        Assert.False(r.FwhmKnown);
        Assert.True(double.IsNaN(r.EffectiveDetailPx));
        Assert.All(r.Rows, row => Assert.True(double.IsNaN(row.StarMm)));
        Assert.Equal(180, r.FallbackDpi);
        Assert.True(Row(r, 80).PointAtViewingDistance);    // 189 dpi
        Assert.False(Row(r, 90).PointAtViewingDistance);   // 168 dpi
        Assert.Equal(80, r.ReferenceRow.WidthCm);
        Assert.False(r.CanRecommend);                      // o limita a fisierului, nu o masuratoare
        Assert.Null(r.RecommendedRow);
        Assert.InRange(Row(r, 80).PixelDistanceM, 0.46, 0.47);   // pas 0.135 mm / 0.29

        // Cu distanta data pragul e cel fizic: de la 1.5 m grila dispare de la 58 dpi in sus.
        var far = PrintSizeAnalyzer.Analyze(W, H, double.NaN, new Parameters { ViewingDistanceM = 1.5 });
        Assert.InRange(far.FallbackDpi, 58.3, 58.5);
        Assert.All(far.Rows, row => Assert.True(row.PointAtViewingDistance));
        Assert.Equal(100, far.ReferenceRow.WidthCm);
        Assert.False(far.CanRecommend);
    }

    [Fact]
    public void WhenNothingPassesTheSmallestIsTheReference()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, 30.0, new Parameters());

        Assert.All(r.Rows, row => Assert.False(row.PointAtViewingDistance));
        Assert.Equal(50, r.ReferenceRow.WidthCm);
    }

    /// <summary>
    /// Scara dpi - distanta: 300 dpi = grila dispare de la 0.29 m (citit), 180 = pas 0.14 mm,
    /// 0.49 m. Cu distanta data, ultimul rand e pragul ei: 58 dpi la 1.5 m.
    /// </summary>
    /// <summary>La 0.25 mm/px, 5938 px sunt ~148 cm la 100 %; printul de referinta la distanta naturala e 100 cm.</summary>
    [Fact]
    public void TheHundredPercentViewIsAPrintOfTheScreensPitch()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, Fwhm, new Parameters());
        Assert.InRange(r.ScreenEquivalentWidthCm, 148.4, 148.5);
        Assert.Equal(100, r.ReferenceRow.WidthCm);
        Assert.InRange(r.ScreenStepBackFactor, 1.48, 1.49);

        var none = PrintSizeAnalyzer.Analyze(W, H, Fwhm, new Parameters { CandidateWidthsCm = System.Array.Empty<double>() });
        Assert.True(double.IsNaN(none.ScreenStepBackFactor));
    }

    [Fact]
    public void TheDpiLadderSaysFromHowFarTheGridVanishes()
    {
        var ladder = PrintSizeAnalyzer.DpiLadder(null);
        Assert.Equal(5, ladder.Count);
        Assert.Equal(300, ladder[0].Dpi);
        Assert.InRange(ladder[0].DistanceM, 0.29, 0.30);
        var at180 = ladder.Single(l => l.Dpi == 180);
        Assert.InRange(at180.PitchMm, 0.14, 0.15);
        Assert.InRange(at180.DistanceM, 0.48, 0.49);

        var given = PrintSizeAnalyzer.DpiLadder(1.5);
        Assert.Equal(6, given.Count);
        Assert.InRange(given[5].Dpi, 58.3, 58.5);
        Assert.InRange(given[5].DistanceM, 1.49, 1.51);
    }

    [Fact]
    public void FrameDimensionsFollowTheParameters()
    {
        var r = PrintSizeAnalyzer.Analyze(W, H, Fwhm, new Parameters { PassepartoutCm = 6, FrameCm = 4 });

        Assert.Equal(60 + 20, Row(r, 60).OuterWidthCm, 6);
    }
}
