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

public class QuantizationAnalyzerTests
{
    static Raster Sixteen(Raster r) { r.Source = new SourceInfo { BitDepth = 16, IsFloat = false }; return r; }

    [Fact]
    public void ANoiselessSlowRampHasLongPlateausThatWouldShowOnPaper()
    {
        // 0.10 -> 0.11 over 256 px: 2.5 levels across the whole width, so under one step per tile.
        var q = QuantizationAnalyzer.Analyze(SyntheticRasters.Ramp(256, 256, 0.10f, 0.11f));

        Assert.True(q.PlateauKnown);
        Assert.True(q.PlateauTiles > 50, "tiles: " + q.PlateauTiles);
        Assert.True(q.PlateauPx > 20, "plateau px: " + q.PlateauPx);
        Assert.True(q.PlateauVisibleAt(200));
        Assert.Equal(q.PlateauPx * 25.4 / 200, q.PlateauMmAt(200), 9);
    }

    [Fact]
    public void ALittleNoiseDithersThePlateausAway()
    {
        var r = SyntheticRasters.AddNoise(SyntheticRasters.Flat(256, 256, 0.10f), 0.5 / 255, 4);
        var q = QuantizationAnalyzer.Analyze(r);

        Assert.True(q.PlateauKnown);
        Assert.InRange(q.PlateauPx, 1.0, 6.0);
        Assert.False(q.PlateauVisibleAt(200));
    }

    [Fact]
    public void ABrightOrStructuredImageHasNoPlateauTiles()
    {
        // A mean over 70 levels: not sky, not measured.
        var q = QuantizationAnalyzer.Analyze(SyntheticRasters.Flat(256, 256, 0.5f));

        Assert.Equal(0, q.PlateauTiles);
        Assert.False(q.PlateauKnown);
        Assert.False(q.PlateauVisibleAt(200));
    }

    [Fact]
    public void RoundingToEightBitsIsHalfALevelAtMost()
    {
        var rng = new SeededRandom(8);
        var r = Sixteen(SyntheticRasters.From(64, 64, 3, (x, y, p) => rng.Next(65536) / 65535f));
        var q = QuantizationAnalyzer.Analyze(r);

        Assert.True(q.Applicable);
        Assert.InRange(q.RoundingMeanAbs, 0.24, 0.26);
        Assert.True(q.RoundingMax <= 0.5001, "max: " + q.RoundingMax);
        Assert.InRange(q.RoundingP999, 0.45, 0.501);
    }

    /// <summary>
    /// An 8-bit file saved as 16 bits: every value is already an 8-bit level, so the lift from
    /// the 16-bit data and from the data rounded to 8 bits miss the same levels, and the master
    /// advice is not given; R-096's upscale warning covers it (ADR 0043).
    /// </summary>
    [Fact]
    public void R082_AnUpscaledSourceMissesTheSameLevelsOnBothPathsAndKeepsNoMaster()
    {
        Raster ramp = SyntheticRasters.Ramp(4, 4096, 0f, 0.5f);
        foreach (float[] plane in ramp.Planes)
            for (int i = 0; i < plane.Length; i++) plane[i] = RasterOps.Level8(plane[i]) * 257 / 65535f;
        var q = QuantizationAnalyzer.Analyze(Sixteen(ramp));

        Assert.True(q.Applicable);
        Assert.Equal(q.MissingFrom16, q.MissingFrom8);
        Assert.True(q.MissingFrom8 > Thresholds.MissingLevelsWarn, "missing from 8 bits: " + q.MissingFrom8);
        Assert.False(q.Keep16BitMaster);
    }

    [Fact]
    public void R082_TheShadowLiftStressShowsWhatEightBitsLoses()
    {
        // A continuous ramp 0 -> 0.5: lifted, it uses every level in the shadows. The same ramp
        // quantised to 8 bits first leaves gaps between them.
        var r = Sixteen(SyntheticRasters.Ramp(4, 4096, 0f, 0.5f));
        var q = QuantizationAnalyzer.Analyze(r);

        Assert.Equal(111, q.LevelsUsedFrom16);
        Assert.Equal(0, q.MissingFrom16);
        Assert.True(q.LevelsUsedFrom8 < 90, "used from 8 bits: " + q.LevelsUsedFrom8);
        Assert.True(q.MissingFrom8 > 20, "missing from 8 bits: " + q.MissingFrom8);
        Assert.True(q.Keep16BitMaster);
    }

    [Fact]
    public void OnAnEightBitSourceThereIsNothingToLose()
    {
        var r = SyntheticRasters.Ramp(4, 4096, 0f, 0.5f);
        r.Source = new SourceInfo { BitDepth = 8, IsFloat = false };
        var q = QuantizationAnalyzer.Analyze(r);

        Assert.False(q.Applicable);
        Assert.False(q.Keep16BitMaster);
        Assert.Equal(0, q.RoundingMax);
    }

    [Fact]
    public void ARasterSmallerThanATileHasNoPlateau()
    {
        var q = QuantizationAnalyzer.Analyze(SyntheticRasters.Flat(32, 32, 0.1f));

        Assert.Equal(0, q.PlateauTiles);
        Assert.False(q.PlateauKnown);
    }

    [Fact]
    public void HistogramPercentileFindsTheBin()
    {
        var hist = new long[10];
        hist[2] = 50; hist[7] = 50;

        Assert.Equal(2, QuantizationAnalyzer.PercentileFromHistogram(hist, 100, 50));
        Assert.Equal(7, QuantizationAnalyzer.PercentileFromHistogram(hist, 100, 99));
    }
}
