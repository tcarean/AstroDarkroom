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

using System.Text;

namespace FlatCheck.Tests;

/// <summary>
/// Phase 7: the hand-rolled PNG encoder, checked with the hand-rolled decoder in
/// <see cref="PngDecoder"/> rather than with an image library - so the two do not share
/// a bug, and the suite stays portable.
/// </summary>
public class PngWriterTests
{
    static byte[] Solid(int w, int h, byte r, byte g, byte b)
    {
        var rgb = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++) { rgb[i * 3] = r; rgb[i * 3 + 1] = g; rgb[i * 3 + 2] = b; }
        return rgb;
    }

    // ---------------------------------------------------------------- container

    [Fact]
    public void StartsWithThePngSignature()
    {
        byte[] png = PngWriter.Encode(Solid(2, 2, 1, 2, 3), 2, 2);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8).ToArray());
    }

    [Fact]
    public void WritesIhdrThenIdatThenIend()
    {
        var d = PngDecoder.Decode(PngWriter.Encode(Solid(4, 3, 10, 20, 30), 4, 3));

        Assert.Equal(new[] { "IHDR", "IDAT", "IEND" }, d.Chunks.Select(c => c.Type).ToArray());
    }

    [Fact]
    public void DeclaresEightBitTruecolourNonInterlaced()
    {
        var d = PngDecoder.Decode(PngWriter.Encode(Solid(4, 3, 0, 0, 0), 4, 3));

        Assert.Equal(8, d.BitDepth);
        Assert.Equal(2, d.ColorType);      // RGB
        Assert.Equal(0, d.Compression);
        Assert.Equal(0, d.Filter);
        Assert.Equal(0, d.Interlace);
    }

    [Fact]
    public void RecordsTheDimensionsInIhdr()
    {
        var d = PngDecoder.Decode(PngWriter.Encode(Solid(7, 5, 0, 0, 0), 7, 5));

        Assert.Equal(7, d.Width);
        Assert.Equal(5, d.Height);
    }

    [Fact]
    public void EveryChunkCrcValidates()
    {
        var d = PngDecoder.Decode(PngWriter.Encode(Solid(16, 16, 200, 100, 50), 16, 16));

        Assert.All(d.Chunks, c => Assert.True(c.CrcValid,
            $"{c.Type} declared {c.DeclaredCrc:X8} but computes to {c.ComputedCrc:X8}"));
    }

    [Fact]
    public void PrefixesEveryRowWithFilterZero()
    {
        var d = PngDecoder.Decode(PngWriter.Encode(Solid(4, 6, 1, 1, 1), 4, 6));

        Assert.Equal(6, d.RowFilters.Count);
        Assert.All(d.RowFilters, f => Assert.Equal(0, f));
    }

    [Fact]
    public void TheIendChunkIsEmpty()
    {
        var d = PngDecoder.Decode(PngWriter.Encode(Solid(2, 2, 0, 0, 0), 2, 2));

        Assert.Empty(d.Chunks.Single(c => c.Type == "IEND").Data);
    }

    // ---------------------------------------------------------------- CRC-32

    /// <summary>The standard CRC-32 check value: "123456789" -> 0xCBF43926.</summary>
    [Fact]
    public void Crc32MatchesTheStandardCheckVector()
    {
        Assert.Equal(0xCBF43926u,
            PngWriter.Crc32(Encoding.ASCII.GetBytes("123456789"), Array.Empty<byte>()));
    }

    /// <summary>An empty IEND chunk has a fixed, well-known CRC in every PNG ever written.</summary>
    [Fact]
    public void TheEmptyIendCrcIsTheWellKnownConstant()
    {
        Assert.Equal(0xAE426082u,
            PngWriter.Crc32(Encoding.ASCII.GetBytes("IEND"), Array.Empty<byte>()));
    }

    // ---------------------------------------------------------------- pixels

    [Fact]
    public void A2x2OfKnownColoursRoundTripsExactly()
    {
        var rgb = new byte[]
        {
            255, 0, 0,      0, 255, 0,
            0, 0, 255,      17, 34, 51,
        };

        var d = PngDecoder.Decode(PngWriter.Encode(rgb, 2, 2));

        Assert.Equal(rgb, d.Rgb);
        Assert.Equal((byte)255, d.At(0, 0).R);
        Assert.Equal((byte)255, d.At(1, 0).G);
        Assert.Equal((byte)255, d.At(0, 1).B);
        Assert.Equal(((byte)17, (byte)34, (byte)51), d.At(1, 1));
    }

    [Fact]
    public void PixelsStayRowMajorOnANonSquareImage()
    {
        // a distinct value per pixel so any transpose or stride error shows up
        int w = 5, h = 3;
        var rgb = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 3;
                rgb[p] = (byte)(x * 10); rgb[p + 1] = (byte)(y * 10); rgb[p + 2] = 7;
            }

        var d = PngDecoder.Decode(PngWriter.Encode(rgb, w, h));

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Assert.Equal(((byte)(x * 10), (byte)(y * 10), (byte)7), d.At(x, y));
    }

    /// <summary>
    /// The CRC table used to be filled lazily, with the field assigned BEFORE the fill:
    /// a second thread arriving in that window read a table of zeros and wrote a wrong
    /// CRC, i.e. a corrupt PNG. Reproduced with 8 threads: 5261 wrong CRCs in 2000
    /// rounds. The window can hit it - the Ratio tab encodes while Structure still runs.
    /// The table is now a static readonly built whole; this pins that many threads
    /// encoding at once all produce the serial bytes, CRCs included.
    /// </summary>
    [Fact]
    public void EncodingOnManyThreadsGivesTheSameBytesAsSerially()
    {
        var rng = new Random(7);
        var rgb = new byte[64 * 48 * 3];
        rng.NextBytes(rgb);
        byte[] expected = PngWriter.Encode(rgb, 64, 48);

        var results = new byte[64][];
        Parallel.For(0, results.Length, i => results[i] = PngWriter.Encode(rgb, 64, 48));

        Assert.All(results, r => Assert.Equal(expected, r));
        Assert.All(results, r => PngDecoder.Decode(r));   // every chunk CRC validates
    }

    [Fact]
    public void WriteProducesTheSameBytesAsEncode()
    {
        using var ws = new TempWorkspace();
        var rgb = Solid(8, 8, 3, 5, 7);
        string path = ws.PathTo("out.png");

        PngWriter.Write(path, rgb, 8, 8);

        Assert.Equal(PngWriter.Encode(rgb, 8, 8), File.ReadAllBytes(path));
    }

    // ---------------------------------------------------------------- colormap

    /// <summary>White is pinned at the midpoint, which every caller arranges to be 1.0.</summary>
    [Fact]
    public void ColormapIsWhiteExactlyAtTheMidpoint()
    {
        Assert.Equal(((byte)255, (byte)255, (byte)255), PngWriter.Colormap(1.0, 0.9, 1.0, 1.1));
    }

    [Fact]
    public void ColormapIsPureBlueAtAndBelowTheLowEnd()
    {
        Assert.Equal(((byte)0, (byte)0, (byte)255), PngWriter.Colormap(0.9, 0.9, 1.0, 1.1));
        Assert.Equal(((byte)0, (byte)0, (byte)255), PngWriter.Colormap(-5, 0.9, 1.0, 1.1));
    }

    [Fact]
    public void ColormapIsPureRedAtAndAboveTheHighEnd()
    {
        Assert.Equal(((byte)255, (byte)0, (byte)0), PngWriter.Colormap(1.1, 0.9, 1.0, 1.1));
        Assert.Equal(((byte)255, (byte)0, (byte)0), PngWriter.Colormap(99, 0.9, 1.0, 1.1));
    }

    [Fact]
    public void ColormapRampsBlueToWhiteBelowTheMidpoint()
    {
        // halfway from lo to mid: blue stays saturated, red and green come up
        var (r, g, b) = PngWriter.Colormap(0.95, 0.9, 1.0, 1.1);

        Assert.Equal((byte)127, r);
        Assert.Equal((byte)127, g);
        Assert.Equal((byte)255, b);
    }

    [Fact]
    public void ColormapRampsWhiteToRedAboveTheMidpoint()
    {
        var (r, g, b) = PngWriter.Colormap(1.05, 0.9, 1.0, 1.1);

        Assert.Equal((byte)255, r);
        Assert.Equal((byte)127, g);
        Assert.Equal((byte)127, b);
    }

    [Fact]
    public void ColormapSurvivesADegenerateRange()
    {
        // lo == mid == hi would divide by zero without the 1e-12 floor
        var (r, g, b) = PngWriter.Colormap(1.0, 1.0, 1.0, 1.0);

        Assert.Equal(((byte)255, (byte)255, (byte)255), (r, g, b));
    }

    // ---------------------------------------------------------------- residual scaling

    /// <summary>Range is the 1st/99th percentile widened by 10% of its own span.</summary>
    [Fact]
    public void MapResidualScalesToThe1And99PercentilesPlusTenPercentPadding()
    {
        // 100 values evenly spaced over [1.00, 1.99]
        var img = new Img(10, 10);
        for (int i = 0; i < 100; i++) img.D[i] = 1.0 + i / 100.0;

        var (_, lo, hi) = PngWriter.MapResidual(img);

        var vals = img.D.ToList();
        double p1 = Stats.Percentile(vals, 1), p99 = Stats.Percentile(vals, 99);
        double pad = (p99 - p1) * 0.1;
        Assert.Equal(p1 - pad, lo, 9);
        Assert.Equal(p99 + pad, hi, 9);
    }

    [Fact]
    public void MapResidualPadsAFlatFieldSoTheRangeNeverCollapses()
    {
        var img = new Img(4, 4);
        Array.Fill(img.D, 1.0);

        var (_, lo, hi) = PngWriter.MapResidual(img);

        Assert.True(hi > lo, "range collapsed to a point");
        // p99 - p1 is 0, so the pad comes from the 1e-4 floor: 1e-5 applied at each end
        Assert.Equal(1.0 - 1e-5, lo, 12);
        Assert.Equal(1.0 + 1e-5, hi, 12);
        Assert.Equal(2e-5, hi - lo, 12);
    }

    /// <summary>
    /// A residual of exactly 1 everywhere must render pure white - that is what makes
    /// "any colour on the map is an error" true for the reader.
    /// </summary>
    [Fact]
    public void AFlatResidualOfOneRendersEntirelyWhite()
    {
        var img = new Img(8, 8);
        Array.Fill(img.D, 1.0);

        var (rgb, _, _) = PngWriter.MapResidual(img);

        Assert.All(rgb, v => Assert.Equal((byte)255, v));
    }

    [Fact]
    public void ValuesBelowOneGoBlueAndAboveOneGoRed()
    {
        var img = new Img(1, 2);
        img.Set(0, 0, 0.5);   // well below
        img.Set(0, 1, 1.5);   // well above

        var (rgb, _, _) = PngWriter.MapResidual(img);

        Assert.True(rgb[2] > rgb[0], "low value should be blue-dominant");
        Assert.True(rgb[3] > rgb[5], "high value should be red-dominant");
    }

    [Fact]
    public void WriteResidualMapProducesADecodableFileOfTheRightSize()
    {
        using var ws = new TempWorkspace();
        var img = TestImages.From(32, 24, FitsTestData.Radial(32, 24, center: 1.0, k: 0.2));
        string path = ws.PathTo("map.png");

        PngWriter.WriteResidualMap(path, img);

        var d = PngDecoder.Decode(File.ReadAllBytes(path));
        Assert.Equal(32, d.Width);
        Assert.Equal(24, d.Height);
        Assert.All(d.Chunks, c => Assert.True(c.CrcValid));
    }

    // ------------------------------------------------------------------- scale

    static Img Residual(int w = 32, int h = 24)
        => TestImages.From(w, h, FitsTestData.Radial(w, h, center: 1.0, k: 0.2));

    /// <summary>
    /// The reason the console output and the saved flatcheck.png did not move when the
    /// window gained a fixed scale: the parameterless entry point still encodes exactly
    /// what it encoded before, byte for byte.
    /// </summary>
    [Fact]
    public void EncodeResidualMapWithTheAutoRangeIsByteIdenticalToTheOldEntryPoint()
    {
        var img = Residual();

        Assert.Equal(
            PngWriter.EncodeResidualMap(img),
            PngWriter.EncodeResidualMap(img, PngWriter.AutoRange(img)));
    }

    [Fact]
    public void TheAutoRangeIsP1P99PlusTenPercentPadding()
    {
        var img = Residual();
        double lo = Stats.Percentile(img.D, 1), hi = Stats.Percentile(img.D, 99);
        double pad = Math.Max(hi - lo, 1e-4) * 0.1;

        var r = PngWriter.AutoRange(img);

        Assert.Equal(lo - pad, r.Lo, 12);
        Assert.Equal(hi + pad, r.Hi, 12);
    }

    [Fact]
    public void AFixedRangeIsSymmetricAroundOne()
    {
        var r = PngWriter.FixedRange(10);

        Assert.Equal(0.9, r.Lo, 12);
        Assert.Equal(1.1, r.Hi, 12);
    }

    /// <summary>
    /// What the fixed scale is FOR: past the cap the colour stops changing, so full red
    /// means "outside this band" instead of "the reddest pixel in this particular image".
    /// </summary>
    [Fact]
    public void AFixedRangeSaturatesBeyondItsCap()
    {
        var img = new Img(1, 4);
        img.Set(0, 0, 0.75);   // below -20%
        img.Set(0, 1, 1.00);   // exactly on the model
        img.Set(0, 2, 1.25);   // above +20%
        img.Set(0, 3, 1.05);   // inside the band

        var (rgb, _, _) = PngWriter.MapResidual(img, PngWriter.FixedRange(20));

        Assert.Equal(new byte[] { 0, 0, 255 }, rgb[0..3]);        // pure blue
        Assert.Equal(new byte[] { 255, 255, 255 }, rgb[3..6]);    // white
        Assert.Equal(new byte[] { 255, 0, 0 }, rgb[6..9]);        // pure red
        Assert.Equal((byte)255, rgb[9]);                          // pink, not saturated
        Assert.InRange(rgb[10], 1, 254);
    }

    /// <summary>
    /// The caps are not numbers of their own - they are the residual scale's own band
    /// boundaries. This is the test that fails if someone recalibrates the scale and
    /// forgets the map, which is exactly how the two would drift apart.
    /// </summary>
    [Fact]
    public void TheFixedCapsAreTheResidualScaleBoundaries()
    {
        Assert.Equal(new[] { Th.ResidPerfect, Th.ResidUsable, Th.ResidCheck }, PngWriter.FixedCapsPct);
        Assert.Equal(Th.ResidUsable, PngWriter.DefaultCapPct);
    }
}
