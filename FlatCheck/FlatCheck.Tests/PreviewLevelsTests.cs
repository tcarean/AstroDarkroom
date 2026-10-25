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
/// The camera-preview side of the exposure analysis. It is pure and takes decoded pixels,
/// so it is tested on synthetic rasters; the one property that matters most is negative -
/// that attaching a preview changes nothing the verdict is built on.
/// </summary>
public class PreviewLevelsTests
{
    const int W = 80, H = 60;

    static PreviewImage Constant(byte r, byte g, byte b)
        => PreviewReader.Decode(TestImages.RgbOf(W, H, (x, y) => (r, g, b)));

    [Fact]
    public void EveryChannelCountsEveryPixel()
    {
        PreviewLevels p = ExposureAnalyzer.AnalyzePreview(Constant(100, 120, 140));

        int window = ((int)(H * 0.6) - (int)(H * 0.4)) * ((int)(W * 0.6) - (int)(W * 0.4));
        Assert.Equal(3, p.Channels.Length);
        foreach (var c in p.Channels)
        {
            Assert.Equal(W * H, c.Whole.Total);
            Assert.Equal(window, c.Centre.Total);
        }
    }

    /// <summary>
    /// Same fraction as the raw side, on both axes, so the two windows are comparable.
    /// </summary>
    [Fact]
    public void TheCentreWindowIsTheSameFractionAsTheRawSide()
    {
        var raw = ExposureAnalyzer.Analyze(TestImages.FlatCfa(128, 128, 550), 1000, null, false);
        PreviewLevels p = ExposureAnalyzer.AnalyzePreview(Constant(1, 2, 3));

        double rawFrac = raw.Channels[0].Centre.Total / (double)raw.Channels[0].Whole.Total;
        double prevFrac = p.Channels[0].Centre.Total / (double)p.Channels[0].Whole.Total;
        Assert.InRange(prevFrac, rawFrac - 0.01, rawFrac + 0.01);
    }

    [Fact]
    public void AConstantPreviewLandsInOneBinPerChannel()
    {
        PreviewLevels p = ExposureAnalyzer.AnalyzePreview(Constant(51, 128, 204));

        Assert.Equal(1, p.Channels[0].Whole.Counts.Count(n => n > 0));
        Assert.Equal(LevelHistogram.BinOf(51 / 255.0 * 100), Array.IndexOf(p.Channels[0].Whole.Counts, W * H));
        Assert.Equal(128 / 255.0 * 100, p.Channels[1].MedianPct, 6);
        Assert.Equal(204 / 255.0 * 100, p.Channels[2].CentreMedianPct, 6);
    }

    [Fact]
    public void TheChannelsAreNamedRGBInThatOrder()
    {
        PreviewLevels p = ExposureAnalyzer.AnalyzePreview(Constant(10, 20, 30));

        Assert.Equal(new[] { "R", "G", "B" }, p.Channels.Select(c => c.Label));
        Assert.Equal(new[] { 'R', 'G', 'B' }, p.Channels.Select(c => c.Colour));
    }

    [Fact]
    public void TheBrightestChannelIsMarked()
    {
        PreviewLevels p = ExposureAnalyzer.AnalyzePreview(Constant(40, 60, 220));

        Assert.Single(p.Channels.Where(c => c.IsBrightest));
        Assert.Equal("B", p.Channels.Single(c => c.IsBrightest).Label);
    }

    /// <summary>
    /// Pixels at 255 are what "the screen is pinned" means, and they must stay visible -
    /// in the last bin and in the clipping share - rather than fall off the axis.
    /// </summary>
    [Fact]
    public void PixelsAt255AreCountedAsClippingAndLandInTheLastBin()
    {
        PreviewLevels p = ExposureAnalyzer.AnalyzePreview(
            PreviewReader.Decode(TestImages.RgbOf(W, H, (x, y) => (x < W / 2 ? (byte)255 : (byte)100, 0, 0))));

        var r = p.Channels[0];
        Assert.InRange(r.ClipPct, 49, 51);
        Assert.Equal(W * H / 2, r.Whole.Counts[LevelHistogram.Bins - 1]);
    }

    /// <summary>
    /// THE test: the preview side never votes. Same pixels, with and without a preview
    /// attached - every figure the verdict is built on is identical.
    /// </summary>
    [Fact]
    public void ThePreviewNeverChangesTheVerdict()
    {
        Img img = TestImages.FlatCfa(128, 128, 550);
        PreviewImage preview = Constant(200, 220, 250);   // an alarming-looking screen

        var without = ExposureAnalyzer.Analyze(img, 1000, null, false);
        var with = ExposureAnalyzer.Analyze(img, 1000, null, false, null, null, preview);

        Assert.Null(without.Preview);
        Assert.NotNull(with.Preview);
        Assert.Equal(without.Verdict, with.Verdict);
        Assert.Equal(without.CentreMedianPct, with.CentreMedianPct, 12);
        Assert.Equal(without.CentreClipPct, with.CentreClipPct, 12);
        Assert.Equal(without.GlobalMedianPct, with.GlobalMedianPct, 12);
        Assert.Equal(without.BrightestChannel, with.BrightestChannel);
        for (int i = 0; i < without.Channels.Length; i++)
            Assert.Equal(without.Channels[i].Whole.Counts, with.Channels[i].Whole.Counts);
    }

    /// <summary>A preview that is present but decoded to null is the same as no preview.</summary>
    [Fact]
    public void ANullPreviewIsSimplyAbsent()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(128, 128, 550), 1000, null, false, null, null, null);

        Assert.Null(r.Preview);
    }

    /// <summary>
    /// Through the run, with the flag: the preview arrives; without it, nothing is decoded.
    /// </summary>
    [Fact]
    public void TheRunDecodesOnlyWhenAsked()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("f.fit", FitsTestData.BuildFits(
            128, 128, FitsTestData.WithBayerMosaic(FitsTestData.Radial(128, 128, center: 34000))));

        // A FITS carries no preview either way; what is asserted is that asking is safe.
        Assert.Null(ExposureRun.Execute(path, null, null, false, new StringWriter()).Result.Preview);
        Assert.Null(ExposureRun.Execute(path, null, null, false, new StringWriter(), withPreview: true).Result.Preview);
    }
}
