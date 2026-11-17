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

public class BitDepthAnalyzerTests
{
    static Raster Sixteen(int h = 64, int w = 64)
    {
        var rng = new SeededRandom(3);
        var r = SyntheticRasters.From(h, w, 3, (x, y, p) => rng.Next(65536) / 65535f);
        r.Source = new SourceInfo { BitDepth = 16, IsFloat = false, PlanesInFile = 3 };
        return r;
    }

    [Fact]
    public void GenuineSixteenBitDataHasManyLevelsAndFewCoincidences()
    {
        var res = BitDepthAnalyzer.Analyze(Sixteen());

        Assert.True(res.Applicable);
        Assert.All(res.DistinctLevels, n => Assert.True(n > 3500, "distinct levels: " + n));
        // The coincidence v == hi*257 happens at 1 in 256 on uniform data.
        Assert.InRange(res.UpscaledFraction, 0.001, 0.01);
        Assert.False(res.IsUpscaled);
    }

    [Fact]
    public void AnEightBitUpscaleIsCaught()
    {
        Raster r = SyntheticRasters.Upscaled8(Sixteen());
        r.Source = new SourceInfo { BitDepth = 16, IsFloat = false, PlanesInFile = 3 };

        var res = BitDepthAnalyzer.Analyze(r);

        Assert.Equal(1.0, res.UpscaledFraction);
        Assert.True(res.IsUpscaled);
        Assert.All(res.DistinctLevels, n => Assert.True(n <= 256));
    }

    /// <summary>An 8-bit copy of the raster saved as 16 bits by a shift: each byte in the high byte, the low byte 0.</summary>
    static Raster Shifted8(Raster src)
    {
        Raster r = SyntheticRasters.From(src.H, src.W, src.Planes.Length, (x, y, p) => (RasterOps.Level8(src.At(p, y, x)) << 8) / 65535f);
        r.Source = new SourceInfo { Format = "memorie", BitDepth = 16, IsFloat = false, PlanesInFile = src.Planes.Length };
        return r;
    }

    /// <summary>
    /// CR-008: only the form level x 257 counted as an upscale, so an 8-bit image saved as 16
    /// bits by a shift (level x 256) passed as real 16 bits; the distinct levels were counted and
    /// reached no rule. At most 256 levels in every plane is 8 bits, whatever the conversion.
    /// </summary>
    [Fact]
    public void R031_AnEightBitImageShiftedIntoTheHighByteIsCaughtByItsLevels()
    {
        var res = BitDepthAnalyzer.Analyze(Shifted8(Sixteen()));

        // Only the level 0 is of the x 257 form too.
        Assert.InRange(res.UpscaledFraction, 0.0, 0.01);
        Assert.All(res.DistinctLevels, n => Assert.InRange(n, 200, 256));
        Assert.True(res.IsUpscaled);
    }

    /// <summary>
    /// R-031: one level more than 8 bits can hold, in one plane, and the levels no longer say
    /// 8 bits.
    /// </summary>
    [Fact]
    public void R031_OneLevelMoreThanEightBitsHoldIsNotAnUpscale()
    {
        Raster r = Shifted8(Sixteen());
        Assert.Equal(256, BitDepthAnalyzer.Analyze(r).DistinctLevels[1]);
        r.Planes[1][0] = 300 / 65535f;

        var res = BitDepthAnalyzer.Analyze(r);

        Assert.Equal(257, res.DistinctLevels[1]);
        Assert.False(res.IsUpscaled);
    }

    /// <summary>
    /// Real 16-bit data with the first <paramref name="black"/> of every plane at pure black and
    /// the next <paramref name="white"/> at pure white, as clipping leaves them.
    /// </summary>
    static Raster Clipped(Raster r, double black, double white)
    {
        foreach (float[] plane in r.Planes)
        {
            int b = (int)(plane.Length * black), w = (int)(plane.Length * white);
            for (int i = 0; i < b; i++) plane[i] = 0f;
            for (int i = b; i < b + w; i++) plane[i] = 1f;
        }
        return r;
    }

    /// <summary>
    /// CR-078: level 0 is its own high byte times 257, and so is 65535, so a clipped sample
    /// counted as "an 8-bit level copied into 16 bits". A real 16-bit frame with 40 % of its
    /// samples at pure black and 15 % at pure white read 55 % of such samples and was called an
    /// 8-bit upscale, with thousands of levels beside the verdict. The share is asked of the
    /// samples that can tell the two apart.
    /// </summary>
    [Fact]
    public void R031_ClippedBlackAndWhiteDoNotMakeAFrameAnUpscale()
    {
        var res = BitDepthAnalyzer.Analyze(Clipped(Sixteen(), 0.40, 0.15));

        Assert.All(res.DistinctLevels, n => Assert.True(n > 1500, "distinct levels: " + n));
        Assert.InRange(res.UpscaledFraction, 0.0, 0.02);
        Assert.False(res.IsUpscaled);
    }

    /// <summary>
    /// ADR 0110, from the review of its branch: of the levels a shift writes (level x 256) only
    /// 0 is also a level x 257, and 0 is left out, so a file saved by a shift has no sample of
    /// the copied form at all. It is 8 bits by its levels alone, with the levels' sentence.
    /// </summary>
    [Fact]
    public void R031_AShiftedFileHasNoSampleOfTheCopiedForm()
    {
        var res = BitDepthAnalyzer.Analyze(Shifted8(Sixteen()));

        Assert.Equal(0.0, res.UpscaledFraction);
        Assert.True(res.FewLevels);
        Assert.True(res.IsUpscaled);
    }

    /// <summary>
    /// R-031: the clipped samples left out, an upscale is still one. A x 257 file that is 60 %
    /// pure black keeps a share of 1 on the samples that can tell; a file of pure black and pure
    /// white alone has none that can, a share of 0, and is 8 bits by its two levels.
    /// </summary>
    [Fact]
    public void R031_AnUpscaleWithClippedAreasIsStillCaught()
    {
        Raster up = SyntheticRasters.Upscaled8(Sixteen());
        up.Source = new SourceInfo { BitDepth = 16, IsFloat = false, PlanesInFile = 3 };
        var res = BitDepthAnalyzer.Analyze(Clipped(up, 0.60, 0.0));
        Assert.Equal(1.0, res.UpscaledFraction);
        Assert.True(res.IsUpscaled);

        var ends = BitDepthAnalyzer.Analyze(Clipped(Sixteen(), 0.50, 0.50));
        Assert.Equal(0.0, ends.UpscaledFraction);
        Assert.All(ends.DistinctLevels, n => Assert.Equal(2, n));
        Assert.True(ends.IsUpscaled);
    }

    [Fact]
    public void NotApplicableOnEightBitSourcesFloatFitsOrNoSource()
    {
        Raster eight = Sixteen(); eight.Source = new SourceInfo { BitDepth = 8, IsFloat = false };
        Raster flt = Sixteen(); flt.Source = new SourceInfo { BitDepth = 32, IsFloat = true };
        Raster none = Sixteen(); none.Source = null;

        Assert.False(BitDepthAnalyzer.Analyze(eight).Applicable);
        Assert.False(BitDepthAnalyzer.Analyze(flt).Applicable);
        Assert.False(BitDepthAnalyzer.Analyze(none).Applicable);
        Assert.False(BitDepthAnalyzer.Analyze(eight).IsUpscaled);
    }
}
