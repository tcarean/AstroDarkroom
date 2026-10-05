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
    public void R041_MidGreyIsBrightAndNeutral()
    {
        var t = TonalAnalyzer.Analyze(SyntheticRasters.Flat(64, 64, 0.5f), Baryta);

        Assert.All(t.MeanLevels, m => Assert.Equal(127.5, m, 3));
        Assert.Equal(0, t.PctBelowDarkA);
        Assert.Equal(0, t.PctBelowDarkB);
        Assert.All(t.ReferencePercentiles, v => Assert.Equal(127.5, v, 3));
        Assert.Equal(0.5, t.BackgroundMedian, 6);
        Assert.Equal(2.4, t.Dmax);
        Assert.Equal(3.60, t.PaperBlackLstar, 1);
        Assert.Equal(53.39, t.LstarMedian, 1);
        Assert.Equal(0, t.PctBelowPaperBlack);
        Assert.Equal(0, t.PctBelowLowSeparation);
        Assert.Equal(100, t.PctAboveHighlight);
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
    }

    [Fact]
    public void TheMediumChangesWhatCountsAsPaperBlack()
    {
        // 0.10: L* ~9, mult sub negrul canvasului (Dmax 1.5, L* ~21), dar peste cel al
        // acrilului (2.2, L* ~6).
        var r = SyntheticRasters.Flat(32, 32, 0.10f);

        Assert.Equal(100, TonalAnalyzer.Analyze(r, new Parameters { Medium = PrintMedium.Canvas }).PctBelowPaperBlack);
        Assert.Equal(0, TonalAnalyzer.Analyze(r, new Parameters { Medium = PrintMedium.Acrylic }).PctBelowPaperBlack);
    }

    /// <summary>
    /// Tabela nivel - L*: cele trei negre, 32/64, L* 15/50 si fundalul-tinta, crescator
    /// dupa L*; mediul ales e marcat, iar un Dmax explicit care nu e al niciunui mediu
    /// isi aduce randul lui.
    /// </summary>
    [Fact]
    public void TheLadderListsEveryReferenceInLstarOrder()
    {
        var ladder = TonalAnalyzer.LstarLadder(new Parameters { TargetBackground = 0.19 });

        Assert.Equal(3 + 2 + 2 + 1, ladder.Count);
        Assert.Equal(ladder.OrderBy(l => l.Lstar).Select(l => l.Lstar), ladder.Select(l => l.Lstar));
        var chosen = Assert.Single(ladder, l => l.Chosen);
        Assert.Contains("baryta", chosen.Meaning);
        Assert.Equal(12.9, chosen.GrayLevel, 1);   // L* 3.6, the black of baryta at Dmax 2.4
        Assert.Equal(12.3, ladder.Single(l => l.GrayLevel == Thresholds.DarkLevelA).Lstar, 1);
        Assert.Equal(20.1, ladder.Single(l => l.Meaning.StartsWith("fundalul-tinta dat 0.19")).Lstar, 1);
        Assert.Equal(3 + 2 + 2, TonalAnalyzer.LstarLadder(Baryta).Count);   // no target given, no simulation: no target row

        var custom = TonalAnalyzer.LstarLadder(new Parameters { DmaxOverride = 1.8 });
        Assert.Equal(8, custom.Count);
        Assert.Contains("Dmax explicit 1.8", Assert.Single(custom, l => l.Chosen).Meaning);
    }

    /// <summary>
    /// The marked row is the paper in use. A Dmax typed on canvas that happens to be acrylic's
    /// is still an explicit Dmax on canvas: it gets its own row, and acrylic's is not "the
    /// chosen medium", which the report beside it calls canvas.
    /// </summary>
    [Fact]
    public void R042_AnExplicitDmaxGetsItsOwnRowEvenWhenItIsAnotherMediumsDmax()
    {
        var ladder = TonalAnalyzer.LstarLadder(new Parameters { Medium = PrintMedium.Canvas, DmaxOverride = 2.2 });

        Assert.Equal(3 + 1 + 2 + 2, ladder.Count);
        Assert.Contains("Dmax explicit 2.2", Assert.Single(ladder, l => l.Chosen).Meaning);
        Assert.False(ladder.Single(l => l.Meaning.Contains("acrilic")).Chosen);

        // The medium's own Dmax, given explicitly, is not explicit: its row is the marked one.
        var own = TonalAnalyzer.LstarLadder(new Parameters { Medium = PrintMedium.Canvas, DmaxOverride = 1.5 });
        Assert.Equal(3 + 2 + 2, own.Count);
        Assert.Contains("canvas", Assert.Single(own, l => l.Chosen).Meaning);
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
