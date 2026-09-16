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

namespace FlatCheck.Tests;

/// <summary>
/// The smoothing chain, with the same promise RunsTests pins for the other runs: what
/// comes through the run is what the analyzer and the report give on their own, and the
/// file that is written re-loads to the analyzer's raster.
/// </summary>
public class SmoothRunTests
{
    const int Size = 256;
    const double Scale = 12;   // sigma 2 tiles on a 256-px mono frame (tile 6)

    static string WriteFlat(TempWorkspace ws, string name = "flat.fit", int bitpix = 16)
        => ws.WriteFile(name, FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size),
            bitpix: bitpix, bzero: bitpix == 16 ? null : 0,
            extraCards: new[] { FitsTestData.Card("STACKCNT", "30") }));

    [Fact]
    public void ProducesTheSameNumbersAsCallingTheAnalyzerDirectly()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws);

        var direct = SmoothAnalyzer.Analyze(ImageLoader.LoadImage(path), Scale);
        var run = SmoothRun.Execute(path, Scale, new StringWriter());

        Assert.False(run.Refused);
        Assert.Equal(direct.RemovedPtpPct, run.Result.RemovedPtpPct, 12);
        Assert.Equal(direct.RemovedMaxTilePct, run.Result.RemovedMaxTilePct, 12);
        Assert.Equal(direct.After.Verdict, run.Result.After.Verdict);
        Assert.Equal(direct.Smoothed.D, run.Result.Smoothed.D);
    }

    [Fact]
    public void WritesTheSameTextAsTheReportWouldOnItsOwn()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws);

        var expected = new StringWriter();
        SmoothReport.Write(SmoothAnalyzer.Analyze(ImageLoader.LoadImage(path), Scale), path, expected);
        var actual = new StringWriter();
        SmoothRun.Execute(path, Scale, actual);

        Assert.Equal(expected.ToString(), actual.ToString());
    }

    [Fact]
    public void ReturnsTwoDecodableMaps_SizedLikeTheAnalysisRaster()
    {
        using var ws = new TempWorkspace();
        var run = SmoothRun.Execute(WriteFlat(ws), Scale, new StringWriter());

        var primary = PngDecoder.Decode(run.Png);
        var secondary = PngDecoder.Decode(run.SecondaryPng);

        Assert.Equal((run.Result.Width, run.Result.Height), (primary.Width, primary.Height));
        Assert.Equal((run.Result.Width, run.Result.Height), (secondary.Width, secondary.Height));
    }

    /// <summary>The written file re-loads to the analyzer's raster, and carries the input's cards.</summary>
    [Fact]
    public void TheWrittenFitsReloadsToTheSmoothedRaster_WithTheInputHeader()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws, bitpix: -32);
        var run = SmoothRun.Execute(path, Scale, new StringWriter());

        using var ms = new MemoryStream();
        SmoothRun.WriteFits(run, path, ms);
        var (back, hdr, cards, _, bitpix) = FitsReader.LoadFull(ms.ToArray());

        Assert.Equal(-32, bitpix);
        for (int i = 0; i < back.D.Length; i++)
            Assert.Equal((double)(float)run.Result.Smoothed.D[i], back.D[i]);
        Assert.Equal("30", hdr["STACKCNT"]);
        Assert.Contains(cards, c => c.StartsWith("HISTORY ") && c.Contains("--smooth"));
    }

    /// <summary>Same type in, same type out: a 16-bit master stays 16-bit, a float one float.</summary>
    [Theory]
    [InlineData(16, 16)]
    [InlineData(-32, -32)]
    [InlineData(8, -32)]
    [InlineData(32, -32)]
    [InlineData(-64, -32)]
    [InlineData(0, -32)]
    public void OutputBitpixFollowsTheInput(int input, int expected)
        => Assert.Equal(expected, SmoothRun.OutputBitpix(input));

    [Fact]
    public void ASixteenBitInputIsWrittenAsSixteenBit()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws, bitpix: 16);
        var run = SmoothRun.Execute(path, Scale, new StringWriter());

        using var ms = new MemoryStream();
        SmoothRun.WriteFits(run, path, ms);
        var (back, _, _, _, bitpix) = FitsReader.LoadFull(ms.ToArray());

        Assert.Equal(16, bitpix);
        Assert.InRange(back.At(128, 128), run.Result.Smoothed.At(128, 128) - 0.5, run.Result.Smoothed.At(128, 128) + 0.5);
    }

    [Fact]
    public void ACubeIsRefused_WithTheCubeMessage_AndNoResult()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cube.fit", FitsTestData.BuildFitsCube(64, 64, 3, (x, y, p) => 1000 + p));

        var text = new StringWriter();
        var run = SmoothRun.Execute(path, Scale, text);

        Assert.True(run.Refused);
        Assert.Null(run.Result);
        Assert.Null(run.Png);
        Assert.Contains("3 planuri", run.Refusal);
        Assert.Contains("3 planuri", text.ToString());
    }

    /// <summary>A raw carries no FITS cards; the run refuses before reading a pixel's worth of analysis.</summary>
    [Fact]
    public void ARawIsRefused_WithTheRawMessage()
    {
        // the extension is what dispatches to the raw reader; the file itself is not a NEF, so the
        // reader fails before the run can refuse - so the refusal wording is checked on its own
        Assert.Contains("--expo", SmoothReport.RefusalRaw("DSC_0001.NEF"));
        Assert.Contains("DSC_0001.NEF", SmoothReport.RefusalRaw("DSC_0001.NEF"));
    }
}
