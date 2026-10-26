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
/// Histogram equalisation, as Siril 1.4.4 does it for its "Histogram" display mode. The
/// tests pin the mechanism (the LUT is the CDF times 255, rounded Siril's way), its
/// defining property (every output level holds the same number of pixels), the reason
/// the mode exists (a 0.3 % ripple becomes tens of grey levels), and the channel plumbing.
/// </summary>
public class HistEqAnalyzerTests
{
    static Img Mono(int w, int h, Func<int, int, double> f) => TestImages.From(w, h, f);

    static readonly CfaPattern Rggb = CfaPattern.Parse("RGGB", "test")!;

    // ---------------------------------------------------------------- the LUT

    /// <summary>
    /// Hand-computed against Siril's loop: values {1, 2, 2, 3}, cumulative 1, 3, 4 over
    /// N = 4, times 255 -> 63.75, 191.25, 255 -> round_to_BYTE gives 64, 191, 255.
    /// Pins both the inclusive cumulative and the (x + 0.5) rounding.
    /// </summary>
    [Fact]
    public void TheLutIsTheInclusiveCdfTimes255_RoundedLikeSiril()
    {
        var lut = HistEqLut.Build(new double[] { 1, 2, 2, 3 });

        Assert.Equal(64, lut.Apply(1));
        Assert.Equal(191, lut.Apply(2));
        Assert.Equal(255, lut.Apply(3));
        Assert.Equal(1, lut.Min);
        Assert.Equal(3, lut.Max);
    }

    /// <summary>
    /// The median read off the histogram, at bin width: {1, 2, 2, 3} -> 2, and on a
    /// 12-bit-like ramp it lands within one bin of the exact value. This is what decides
    /// CFA-or-not, instead of sorting four 6 M-value copies.
    /// </summary>
    [Fact]
    public void TheHistogramMedianIsWithinABinOfTheExactOne()
    {
        // at the width of one bin: 2 sits in bin 32767 of 65535, that is at 1.99997
        Assert.InRange(HistEqLut.Build(new double[] { 1, 2, 2, 3 }).Median(), 2.0 - 2.0 / 65535, 2.0);

        var ramp = Enumerable.Range(0, 4001).Select(i => (double)(100 + i)).ToArray();   // 100..4100
        double exact = Stats.Percentile(ramp, 50);
        Assert.True(Math.Abs(HistEqLut.Build(ramp).Median() - exact) < 4000.0 / 65535 + 1e-9);

        Assert.Equal(0, HistEqLut.Build(new double[] { 0, 0 }).Median());
        Assert.Equal(7, HistEqLut.Build(new double[] { 7, 7, 7 }).Median());
    }

    [Theory]
    [InlineData(63.5, 64)]
    [InlineData(63.49, 63)]
    [InlineData(254.5, 255)]
    [InlineData(300, 255)]
    [InlineData(0, 0)]
    public void RoundToByte_IsSirilsRoundToByte(double x, byte expected)
        => Assert.Equal(expected, HistEqLut.RoundToByte(x));

    // ---------------------------------------------------------------- the rainbow table

    /// <summary>
    /// Hand-computed against Siril 1.4.4's make_index_for_rainbow + hsv_to_rgb: the hue runs
    /// from 300 degrees at level 0 down to 0 at level 255, saturation and value 1, integer
    /// sectors, round_to_BYTE. Level 0 is magenta, not black; the in-between levels pin the
    /// sector arithmetic (1 -> 250,0,255; 64 -> 0,65,255; 128 -> 0,255,125; 254 -> 255,5,0).
    /// </summary>
    [Theory]
    [InlineData(0, 255, 0, 255)]
    [InlineData(1, 250, 0, 255)]
    [InlineData(51, 0, 0, 255)]
    [InlineData(64, 0, 65, 255)]
    [InlineData(102, 0, 255, 255)]
    [InlineData(128, 0, 255, 125)]
    [InlineData(153, 0, 255, 0)]
    [InlineData(204, 255, 255, 0)]
    [InlineData(254, 255, 5, 0)]
    [InlineData(255, 255, 0, 0)]
    public void R104_TheRainbowTableIsSirils(int level, byte r, byte g, byte b)
    {
        Assert.Equal((r, g, b), RainbowLut.Of(level));
        Assert.Equal(new[] { r, g, b }, RainbowLut.Table.Skip(level * 3).Take(3));
    }

    /// <summary>
    /// Colouring is the table at every pixel, three bytes per pixel in RGB order: a 256-level
    /// ramp comes out as the table itself, and a few picked levels land where they should.
    /// </summary>
    [Fact]
    public void R104_AColouredRasterIsTheTableAtEveryPixel()
    {
        byte[] ramp = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        Assert.Equal(RainbowLut.Table, RainbowLut.Colour(ramp));
        Assert.Equal(768, RainbowLut.Table.Length);
        Assert.Equal(new byte[] { 255, 0, 255, 255, 0, 0, 0, 0, 255 },
                     RainbowLut.Colour(new byte[] { 0, 255, 51 }));
    }

    /// <summary>A uniform distribution equalises to itself: the ramp comes out linear.</summary>
    [Fact]
    public void AUniformRampComesOutAsALinearRamp()
    {
        var img = Mono(64, 64, (x, y) => y * 64 + x + 1);        // 1..4096, no zeros, no ties

        var r = HistEqAnalyzer.Analyze(img, null);

        Assert.False(r.IsCfa);
        byte[] eq = r.Channels[0].Equalised;
        for (int i = 0; i < eq.Length; i++)
        {
            double expected = (i + 1) / 4096.0 * 255.0;
            Assert.True(Math.Abs(eq[i] - expected) <= 1.0, $"pixel {i}: {eq[i]} vs {expected:F2}");
        }
        for (int i = 1; i < eq.Length; i++) Assert.True(eq[i] >= eq[i - 1], "the LUT must be monotonic");
    }

    /// <summary>
    /// The defining property: every output level receives the same share of pixels. A
    /// continuous input (no ties) on a mono frame; the tolerance covers bin collisions.
    /// </summary>
    [Fact]
    public void EveryOutputLevelHoldsTheSameNumberOfPixels()
    {
        var radial = FitsTestData.Radial(256, 256);
        var img = Mono(256, 256, (x, y) => radial(x, y) * (1.0 + 1e-7 * (y * 256 + x)));

        var r = HistEqAnalyzer.Analyze(img, null);

        var counts = new int[256];
        foreach (byte b in r.Channels[0].Equalised) counts[b]++;
        double expected = 256 * 256 / 256.0;
        for (int l = 1; l < 255; l++)
            Assert.True(Math.Abs(counts[l] - expected) <= 12, $"level {l} holds {counts[l]} pixels, expected ~{expected}");
        Assert.Equal(256, r.Channels[0].LevelsUsed);
    }

    // ---------------------------------------------------------------- what the mode is for

    /// <summary>
    /// A 0.3 % ripple on a realistic flat (5 % of vignetting, a fast lens) is a third of
    /// a grey level in the linear display - the file's own 16-bit scale, which is what a
    /// linear view shows - and around fifteen levels once equalised. Measured as the
    /// difference between the rendering with and without the ripple, along the row
    /// through the centre, so the vignetting itself cancels.
    ///
    /// Not asserted against a min..max linear stretch on purpose: on a field whose
    /// distribution is nearly uniform across its range, equalisation IS that stretch.
    /// What the mode adds over it is adaptivity - every image gets its own curve, and a
    /// peaked distribution gets the steepest one - and that is a property of real,
    /// noisy flats rather than of this synthetic.
    /// </summary>
    [Fact]
    public void ATinyRippleInvisibleInTheLinearViewBecomesManyGreyLevels()
    {
        const int W = 512, H = 512;
        var radial = FitsTestData.Radial(W, H, center: 30000, k: 0.05);
        Func<int, int, double> ripple = (x, y) => 1.0 + 0.003 * Math.Sin(6 * Math.PI * x / W);
        var clean = Mono(W, H, radial);
        var rippled = Mono(W, H, (x, y) => radial(x, y) * ripple(x, y));

        var eqClean = HistEqAnalyzer.Analyze(clean, null).Channels[0].Equalised;
        var eqRippled = HistEqAnalyzer.Analyze(rippled, null).Channels[0].Equalised;

        const double FileScale = 65535.0;
        int row = H / 2;
        double linearPtp = 0, equalisedPtp = 0;
        for (int x = 0; x < W; x++)
        {
            double lin = (rippled.At(row, x) - clean.At(row, x)) / FileScale * 255;
            linearPtp = Math.Max(linearPtp, Math.Abs(lin));
            equalisedPtp = Math.Max(equalisedPtp, Math.Abs(eqRippled[row * W + x] - eqClean[row * W + x]));
        }

        Assert.True(linearPtp < 1.0, $"linear rendering moved by {linearPtp:F2} levels");
        Assert.True(equalisedPtp >= 10, $"equalised rendering moved by only {equalisedPtp:F1} levels");
    }

    // ---------------------------------------------------------------- invariances and Siril quirks

    /// <summary>
    /// Every figure in the tool is a ratio, and this one is rank-based: a gain changes
    /// nothing. A power-of-two gain is exact in floating point, so the output is
    /// bit-identical; an arbitrary gain can move a pixel sitting on a bin boundary by
    /// one bin, which is at most one output level.
    /// </summary>
    [Fact]
    public void AGainChangesNothing()
    {
        var img = TestImages.FlatCfa(96, 64, 1000);
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(96, 64, center: 3000));
        img = Mono(96, 64, scene);
        var x4 = Mono(96, 64, (x, y) => scene(x, y) * 4.0);
        var x73 = Mono(96, 64, (x, y) => scene(x, y) * 7.3);

        var a = HistEqAnalyzer.Analyze(img, Rggb);
        var b = HistEqAnalyzer.Analyze(x4, Rggb);
        var c = HistEqAnalyzer.Analyze(x73, Rggb);

        Assert.Equal(a.CompositeRgb, b.CompositeRgb);
        Assert.Equal(a.Mosaic, b.Mosaic);
        for (int i = 0; i < 4; i++) Assert.Equal(a.Channels[i].Equalised, b.Channels[i].Equalised);
        for (int i = 0; i < a.CompositeRgb.Length; i++)
            Assert.True(Math.Abs(a.CompositeRgb[i] - c.CompositeRgb[i]) <= 1);
    }

    /// <summary>
    /// Siril leaves zero-valued pixels out of the histogram but keeps them in the
    /// denominator, so a frame with dead pixels never reaches 255. Mirrored exactly.
    /// </summary>
    [Fact]
    public void ZerosAreSkippedButStillCounted_AsInSiril()
    {
        var img = Mono(100, 100, (x, y) => y < 10 ? 0.0 : y * 100 + x);   // 10 % zeros

        var r = HistEqAnalyzer.Analyze(img, null);

        byte[] eq = r.Channels[0].Equalised;
        Assert.All(eq.Take(1000), b => Assert.Equal(0, b));
        Assert.Equal(HistEqLut.RoundToByte(0.9 * 255), eq.Max());   // 230, not 255
        Assert.Equal(9000, r.Channels[0].NonZero);
    }

    [Fact]
    public void AConstantFrameMapsTo255_AndAnAllZeroFrameTo0()
    {
        var constant = HistEqAnalyzer.Analyze(Mono(16, 16, (x, y) => 42.0), null);
        var zeros = HistEqAnalyzer.Analyze(Mono(16, 16, (x, y) => 0.0), null);

        Assert.All(constant.Channels[0].Equalised, b => Assert.Equal(255, b));
        Assert.All(zeros.Channels[0].Equalised, b => Assert.Equal(0, b));
        Assert.Equal(0, zeros.Channels[0].NonZero);
    }

    // ---------------------------------------------------------------- channels

    [Fact]
    public void ACfaFrameSplitsIntoFourChannelsOfHalfSize()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.FlatCfa(64, 48, 1000), Rggb);

        Assert.True(r.IsCfa);
        Assert.Equal(4, r.Channels.Length);
        Assert.All(r.Channels, c => { Assert.Equal(24, c.H); Assert.Equal(32, c.W); });
        Assert.Equal(new[] { "R", "G1", "G2", "B" }, r.Channels.Select(c => c.Label));
        Assert.Equal(new[] { 'R', 'G', 'G', 'B' }, r.Channels.Select(c => c.Colour));
        Assert.NotNull(r.Mosaic);
        Assert.Equal(24 * 32, r.Mosaic!.Length);
        Assert.Equal(24 * 32 * 3, r.CompositeRgb.Length);
    }

    [Fact]
    public void WithoutAPattern_TheChannelsAreNamedByPosition_AndTheCompositeIsGrey()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.FlatCfa(64, 48, 1000), null);

        Assert.Equal(new[] { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" }, r.Channels.Select(c => c.Label));
        Assert.All(r.Channels, c => Assert.Equal('\0', c.Colour));
        for (int i = 0; i < r.W * r.H; i++)
        {
            Assert.Equal(r.CompositeRgb[i * 3], r.CompositeRgb[i * 3 + 1]);
            Assert.Equal(r.CompositeRgb[i * 3], r.CompositeRgb[i * 3 + 2]);
        }
    }

    /// <summary>
    /// The composite takes red from the position that IS red, not from position 0. On a
    /// GBRG mosaic red sits at position 2; a ramp put there must come out in the red
    /// channel while the constant positions come out as 255 in green and blue.
    /// </summary>
    [Fact]
    public void TheCompositePicksChannelsByColour_NotByPosition()
    {
        var gbrg = CfaPattern.Parse("GBRG", "test")!;
        var img = Mono(64, 64, (x, y) => Mosaic.PositionOf(y, x) == 2 ? y * 64 + x + 1 : 500.0);

        var r = HistEqAnalyzer.Analyze(img, gbrg);

        Assert.Equal("R", r.Channels[2].Label);
        int n = r.W * r.H;
        var reds = Enumerable.Range(0, n).Select(i => r.CompositeRgb[i * 3]).Distinct().Count();
        Assert.True(reds > 100, "the red channel should carry the ramp");
        Assert.All(Enumerable.Range(0, n), i =>
        {
            Assert.Equal(255, r.CompositeRgb[i * 3 + 1]);
            Assert.Equal(255, r.CompositeRgb[i * 3 + 2]);
        });
    }

    [Fact]
    public void AMonoFrameHasOneChannelAndNoSeparateMosaic()
    {
        var r = HistEqAnalyzer.Analyze(Mono(64, 64, FitsTestData.Radial(64, 64)), Rggb);

        Assert.False(r.IsCfa);
        Assert.Single(r.Channels);
        Assert.Equal("mono", r.Channels[0].Label);
        Assert.Null(r.Mosaic);
        for (int i = 0; i < r.W * r.H; i++)
            Assert.Equal(r.Channels[0].Equalised[i], r.CompositeRgb[i * 3 + 1]);
    }

    // ---------------------------------------------------------------- display size

    [Theory]
    [InlineData(3000, 2000, 2)]
    [InlineData(2399, 1600, 1)]
    [InlineData(500, 300, 1)]
    [InlineData(1200, 4800, 4)]
    public void TheShrinkFactorIsThePrepareRule(int h, int w, int k)
        => Assert.Equal(k, HistEqAnalyzer.ShrinkFactor(h, w, 1200));

    /// <summary>
    /// A 4800 x 200 CFA frame: channels 2400 x 100 shrink by 2 to 1200 x 50, and the
    /// mosaic view, shrunk by 4 from the full frame, lands on exactly the same size.
    /// </summary>
    [Fact]
    public void EveryViewComesOutTheSameSize_AfterShrinking()
    {
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(4800, 200, center: 3000));

        var r = HistEqAnalyzer.Analyze(Mono(4800, 200, scene), Rggb);

        Assert.Equal(2, r.Shrink);
        Assert.Equal(1200, r.W);
        Assert.Equal(50, r.H);
        Assert.All(r.Channels, c => Assert.Equal(1200 * 50, c.Equalised.Length));
        Assert.Equal(1200 * 50, r.Mosaic!.Length);
        Assert.Equal(1200 * 50 * 3, r.CompositeRgb.Length);

        // ...and the full rasters, which the window shows
        Assert.Equal((2400, 100), (r.FullW, r.FullH));
        Assert.All(r.Channels, c => { Assert.Equal(2400 * 100, c.Full.Length); Assert.Equal((2400, 100), (c.FullW, c.FullH)); });
        Assert.Equal(4800 * 200, r.MosaicFull!.Length);
        Assert.Equal(2400 * 100 * 3, r.CompositeFullRgb.Length);
    }

    /// <summary>
    /// The shrunk rasters are derived from the full ones, not recomputed: the k x k block
    /// mean of Full, rounded Siril's way, is Equalised byte for byte - and the same for the
    /// mosaic with 2k. If the two ever came from different LUTs this would catch it.
    /// </summary>
    [Fact]
    public void TheShrunkRastersAreTheBlockMeansOfTheFullOnes()
    {
        var scene = FitsTestData.WithBayerMosaic(FitsTestData.Radial(4800, 200, center: 3000));
        var r = HistEqAnalyzer.Analyze(Mono(4800, 200, scene), Rggb);

        static byte[] BlockMean(byte[] g, int w, int h, int k)
        {
            int ow = w / k, oh = h / k;
            var o = new byte[ow * oh];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int s = 0;
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            s += g[(y * k + dy) * w + x * k + dx];
                    o[y * ow + x] = HistEqLut.RoundToByte(s / (double)(k * k));
                }
            return o;
        }

        foreach (var c in r.Channels)
            Assert.Equal(BlockMean(c.Full, c.FullW, c.FullH, r.Shrink), c.Equalised);
        Assert.Equal(BlockMean(r.MosaicFull!, r.SourceW, r.SourceH, 2 * r.Shrink), r.Mosaic);
    }

    /// <summary>
    /// On a small frame nothing is shrunk, so Full IS Equalised and the mosaic is the frame.
    /// </summary>
    [Fact]
    public void WithoutShrinkingTheFullRastersAreTheDisplayedOnes()
    {
        var r = HistEqAnalyzer.Analyze(TestImages.FlatCfa(64, 48, 1000), Rggb);

        Assert.All(r.Channels, c => { Assert.Same(c.Full, c.Equalised); Assert.Equal((32, 24), (c.FullW, c.FullH)); });
        Assert.Equal(64 * 48, r.MosaicFull!.Length);
        Assert.Equal(r.CompositeRgb, r.CompositeFullRgb);

        var mono = HistEqAnalyzer.Analyze(Mono(40, 30, FitsTestData.Radial(40, 30)), null);
        Assert.Null(mono.MosaicFull);
        Assert.Equal((40, 30), (mono.FullW, mono.FullH));
    }

    [Fact]
    public void ASmallFrameIsNotShrunk()
    {
        var r = HistEqAnalyzer.Analyze(Mono(300, 200, FitsTestData.Radial(300, 200)), null);

        Assert.Equal(1, r.Shrink);
        Assert.Equal(300, r.W);
        Assert.Equal(200, r.H);
    }
}
