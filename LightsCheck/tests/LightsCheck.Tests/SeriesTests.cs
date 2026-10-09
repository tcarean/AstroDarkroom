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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>The whole series on the synthetic eight-frame session (R-022 to R-028).</summary>
public class SeriesTests
{
    private static SeriesResult RunPattern(int biases = 0)
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: biases);
        return SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));
    }

    [Fact]
    public void R026_EveryFrameIsMeasuredAndRegisteredToTheFirst()
    {
        SeriesResult r = RunPattern();
        Assert.Equal(8, r.Frames.Count);
        Assert.All(r.Frames, f => Assert.NotNull(f.Measurement));
        Assert.All(r.Frames, f => Assert.True(f.Registration!.Ok));
        Assert.Equal(1, r.ReferenceIndex);
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(SessionFixture.Pattern[i].Dx, r.Frames[i].Registration!.Dx, 0.3);
            Assert.Equal(SessionFixture.Pattern[i].Dy, r.Frames[i].Registration!.Dy, 0.3);
        }

        Assert.Equal(206.265 * 3.92 / 135, r.ArcsecPerPixel!.Value, 0.01);
        Assert.Equal("din fisier (XPIXSZ)", r.PixelSizeSource);
    }

    [Fact]
    public void R022_TrailedFramesAreBadAndCarryTheirReason()
    {
        SeriesResult r = RunPattern();
        SeriesAnalysis s = r.Series;
        Assert.Equal(FrameClass.Bad, s.Classes[2]);
        Assert.Equal(FrameClass.Bad, s.Classes[5]);
        Assert.Equal(2, s.BadCount);
        Assert.Equal(6, s.GoodCount);
        FrameFlag flag = Assert.Single(s.Flags[2]);
        Assert.Equal(FlagKind.Trailed, flag.Kind);
        Assert.InRange(flag.Value, 1.6, 2.2);
        Assert.Equal(Th.ElongationBad, flag.Threshold);
        Assert.Empty(s.Flags[0]);
    }

    [Fact]
    public void R019_TheDitherPatternIsReadBack()
    {
        DitherAnalysis d = RunPattern().Dither;
        Assert.Equal([false, false, true, false, true, false, true, false], d.IsMove);
        Assert.Equal(3, d.Moves);
        Assert.Equal(3, d.Pauses);
        Assert.Equal(0, d.PausesWithoutMove);
        Assert.Equal(2, d.FramesBetweenMoves[2]);
        Assert.Equal(2, d.MovesOnXOnly);
        Assert.Equal(1, d.MovesOnYOnly);
        Assert.Equal(4, d.DistinctLoose);
    }

    [Fact]
    public void R023_TrendsAndDriftOnAStableSession()
    {
        SeriesAnalysis s = RunPattern().Series;
        Assert.True(s.FocusHeld);
        Assert.False(s.DewSuspected);
        Assert.InRange(Math.Abs(s.DriftXPerMin!.Value), 0, 0.5);
        Assert.InRange(Math.Abs(s.RotationTotalDeg!.Value), 0, 0.05);
        Assert.Equal(Axis.Y, s.RaAxis);   // the trailed frames trail vertically
        Assert.Contains("darelor", s.AxisSource, StringComparison.Ordinal);
    }

    [Fact]
    public void R024_ReadNoiseFromTheBiasesGivesTheSwampingRatios()
    {
        SeriesResult r = RunPattern(biases: 2);
        SeriesAnalysis s = r.Series;
        Assert.NotNull(s.ReadNoise);
        Assert.All(s.ReadNoise!, n => Assert.InRange(n.PairSigma, 10, 14));   // 12 ADU of noise drawn into the biases
        Assert.NotNull(s.Swamping);
        Assert.Equal(3, s.SwampingFrames.Count);
        Assert.All(s.Swamping!.SelectMany(x => x), c => Assert.True(c.SwampingRatio > 3, $"{c.Name} ratio {c.SwampingRatio}"));
    }

    [Fact]
    public void R028_TheOutputsAreWrittenWhereAskedAndNeverIntoLights()
    {
        using SessionFixture s = new SessionFixture().WithStarSeries(SessionFixture.Pattern);
        using TempWorkspace outDir = new();
        SeriesResult r = SeriesRun.Run(s.Root, new SeriesOptions(Threads: 2));
        IReadOnlyList<string> written = SeriesOutputs.WriteAll(r, outDir.Dir);

        Assert.Equal(3, written.Count);
        Assert.All(written, p => Assert.True(File.Exists(p)));
        Assert.Equal("DSC_0003.fit\nDSC_0006.fit\n", File.ReadAllText(Path.Combine(outDir.Dir, SeriesOutputs.RejectedName)));
        string[] csv = File.ReadAllLines(Path.Combine(outDir.Dir, SeriesOutputs.CsvName));
        Assert.Equal(9, csv.Length);
        Assert.StartsWith("index,file,time,exposure_s", csv[0], StringComparison.Ordinal);
        // The header and a row without quoted commas (a good frame, no reasons) have the same number of columns.
        int columns = csv[0].Split(',').Length;
        Assert.Equal(57, columns);
        Assert.Equal(columns, csv[1].Split(',').Length);
        Assert.StartsWith("3,\"DSC_0003.fit\",", csv[3], StringComparison.Ordinal);
        Assert.Contains("SLAB", csv[3], StringComparison.Ordinal);
        Assert.Contains("=== SESIUNE:", File.ReadAllText(Path.Combine(outDir.Dir, SeriesOutputs.ReportName)), StringComparison.Ordinal);
        Assert.Equal(8, Directory.GetFiles(s.LightsDir).Length);
    }

    [Fact]
    public void R025_TheRaAxisComesFromAPlateSolvedFitsAtTheRootWhenThereIsOne()
    {
        using SessionFixture s = new SessionFixture().WithLights(1);
        Assert.Equal(Axis.Unknown, SeriesRun.WcsRaAxis(s.Root));
        string[] wcs =
        [
            FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"),
            FitsTestData.Card("PC1_1", "0.0037"), FitsTestData.Card("PC1_2", "-0.99999"),
        ];
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), FitsTestData.BuildFits(8, 8, (x, y) => 1, extraCards: wcs));
        Assert.Equal(Axis.Y, SeriesRun.WcsRaAxis(s.Root));
    }
}
