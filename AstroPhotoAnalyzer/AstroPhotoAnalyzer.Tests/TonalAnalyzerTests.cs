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

public class TonalAnalyzerTests
{
    static readonly Parameters Baryta = new();

    [Fact]
    public void MidGreyIsBrightNeutralAndPrintSafe()
    {
        var t = TonalAnalyzer.Analyze(SyntheticRasters.Flat(64, 64, 0.5f), Baryta);

        Assert.All(t.MeanLevels, m => Assert.Equal(127.5, m, 3));
        Assert.Equal(0, t.PctBelowDarkA);
        Assert.Equal(0, t.PctBelowDarkB);
        Assert.All(t.ReferencePercentiles, v => Assert.Equal(127.5, v, 3));
        Assert.Equal(0.5, t.BackgroundMedian, 6);
        Assert.Equal(2.0, t.Dmax);
        Assert.Equal(8.99, t.PaperBlackLstar, 1);
        Assert.Equal(53.39, t.LstarMedian, 1);
        Assert.Equal(0, t.PctBelowPaperBlack);
        Assert.Equal(0, t.PctBelowLowSeparation);
        Assert.Equal(100, t.PctAboveHighlight);
        Assert.True(t.PrintSafe);
        Assert.False(t.BackgroundTooLowForPaper);
    }

    [Fact]
    public void TheHistogramSumsToOneHundredAndPeaksAtTheOnlyLstar()
    {
        var t = TonalAnalyzer.Analyze(SyntheticRasters.Flat(64, 64, 0.5f), Baryta);

        Assert.Equal(101, t.LstarHistogramPct.Count);
        Assert.Equal(100.0, t.LstarHistogramPct.Sum(), 6);
        Assert.Equal(100.0, t.LstarHistogramPct[53], 6);
    }

    [Fact]
    public void ADeepShadowImageIsEntirelyBelowPaperBlack()
    {
        // 0.02 e nivelul 5: L* ~1.4, sub negrul oricarei hartii.
        var t = TonalAnalyzer.Analyze(SyntheticRasters.Flat(64, 64, 0.02f), Baryta);

        Assert.Equal(100, t.PctBelowDarkA);
        Assert.Equal(100, t.PctBelowPaperBlack);
        Assert.Equal(100, t.PctBelowLowSeparation);
        Assert.True(t.BackgroundTooLowForPaper);
        Assert.False(t.PrintSafe);
    }

    [Fact]
    public void ATwoToneImageSplitsThePercentages()
    {
        // Jumatatea stanga la 0.02, dreapta la 0.5; pasul 4 ancorat la 0 imparte egal.
        var r = SyntheticRasters.From(64, 64, 3, (x, y, p) => x < 32 ? 0.02f : 0.5f);
        var t = TonalAnalyzer.Analyze(r, Baryta);

        Assert.Equal(50, t.PctBelowPaperBlack, 6);
        Assert.Equal(50, t.PctAboveHighlight, 6);
        Assert.Equal(50, t.PctBelowDarkB, 6);
        Assert.Equal(50, t.PctBelowDarkA, 6);
        Assert.False(t.PrintSafe);
        Assert.True(t.BackgroundTooLowForPaper);
    }

    [Fact]
    public void TheMediumChangesWhatCountsAsPaperBlack()
    {
        // 0.10: L* ~9, exact la negrul baryta-ului (Dmax 2.0) si mult sub cel al
        // canvasului (Dmax 1.5, L* ~21), dar peste cel al acrilului (2.2, L* ~6).
        var r = SyntheticRasters.Flat(32, 32, 0.10f);

        Assert.Equal(100, TonalAnalyzer.Analyze(r, new Parameters { Medium = PrintMedium.Canvas }).PctBelowPaperBlack);
        Assert.Equal(0, TonalAnalyzer.Analyze(r, new Parameters { Medium = PrintMedium.Acrylic }).PctBelowPaperBlack);
    }

    [Fact]
    public void MonoUsesItsOnlyPlaneAsReference()
    {
        var t = TonalAnalyzer.Analyze(SyntheticRasters.Flat(32, 32, 0.25f, planes: 1), Baryta);

        Assert.Single(t.MeanLevels);
        Assert.Equal(63.75, t.MeanLevels[0], 3);
        Assert.Equal(0.25, t.BackgroundMedian, 6);
    }
}
