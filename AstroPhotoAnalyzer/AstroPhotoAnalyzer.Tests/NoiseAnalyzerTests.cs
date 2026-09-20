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

public class NoiseAnalyzerTests
{
    static readonly Parameters P = new();

    static Raster Sky(double sigmaLsb, int seed = 5)
        => SyntheticRasters.AddNoise(SyntheticRasters.Flat(256, 256, 0.3f), sigmaLsb / 255.0, seed);

    [Fact]
    public void RecoversTheInjectedSigma()
    {
        var n = NoiseAnalyzer.Analyze(Sky(1.0), P);

        Assert.False(n.Insufficient);
        Assert.True(n.SmoothTiles > 100, "dale netede: " + n.SmoothTiles);
        Assert.Equal(1.0, n.SigmaMedianLsb, 1);
        Assert.InRange(n.SigmaMinLsb, 0.8, 1.0);
        Assert.Equal(0, n.PctTilesBelowBanding);
        Assert.True(n.GoodForPrint);
        Assert.False(n.BandingRisk);
        Assert.False(n.UnderDenoised);
    }

    [Fact]
    public void ADenoisedSkyIsABandingRisk()
    {
        var n = NoiseAnalyzer.Analyze(Sky(0.3), P);

        Assert.Equal(0.3, n.SigmaMedianLsb, 1);
        Assert.True(n.BandingRisk);
        Assert.Equal(100, n.PctTilesBelowBanding);
        Assert.Equal(100, n.PctTilesBelowOne);
    }

    [Fact]
    public void ANoisySkyIsUnderDenoised()
    {
        var n = NoiseAnalyzer.Analyze(Sky(3.0), P);

        Assert.Equal(3.0, n.SigmaMedianLsb, 0);
        Assert.True(n.UnderDenoised);
        Assert.False(n.GoodForPrint);
    }

    [Fact]
    public void AStrongGradientLeavesNoSmoothTiles()
    {
        // 0..1 pe 256 px: o dala de 64 acopera 0.25 = 64 de niveluri, mult peste prag.
        var n = NoiseAnalyzer.Analyze(SyntheticRasters.Ramp(256, 256, 0f, 1f), P);

        Assert.Equal(0, n.SmoothTiles);
        Assert.True(n.Insufficient);
        Assert.True(double.IsNaN(n.SigmaMedianLsb));
        Assert.False(n.BandingRisk);
        Assert.False(n.GoodForPrint);
    }

    [Fact]
    public void AGentleGradientStillMeasuresTheNoise()
    {
        // 0.3 -> 0.32 pe 256 px: 5 niveluri pe toata latimea, o dala vede ~1.3. Diferenta
        // intre vecini scoate panta; sigma ramane cea injectata.
        var r = SyntheticRasters.AddNoise(SyntheticRasters.Ramp(256, 256, 0.30f, 0.32f), 1.0 / 255, 9);
        var n = NoiseAnalyzer.Analyze(r, P);

        Assert.False(n.Insufficient);
        Assert.Equal(1.0, n.SigmaMedianLsb, 1);
    }

    [Fact]
    public void ARasterSmallerThanATileHasNoCandidates()
    {
        var n = NoiseAnalyzer.Analyze(SyntheticRasters.Flat(32, 32, 0.3f), P);

        Assert.Equal(0, n.Candidates);
        Assert.True(n.Insufficient);
    }

    [Fact]
    public void TheSameSeedPicksTheSameTiles()
    {
        var a = NoiseAnalyzer.Analyze(Sky(1.0), new Parameters { Seed = 0 });
        var b = NoiseAnalyzer.Analyze(Sky(1.0), new Parameters { Seed = 0 });
        var c = NoiseAnalyzer.Analyze(Sky(1.0), new Parameters { Seed = 1 });

        Assert.Equal(a.Tiles.Select(t => (t.X, t.Y)), b.Tiles.Select(t => (t.X, t.Y)));
        Assert.NotEqual(a.Tiles.Select(t => (t.X, t.Y)), c.Tiles.Select(t => (t.X, t.Y)));
        Assert.Equal(a.SigmaMedianLsb, b.SigmaMedianLsb);
    }
}
