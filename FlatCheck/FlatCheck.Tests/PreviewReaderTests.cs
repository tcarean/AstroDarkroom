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

using System.Reflection;

namespace FlatCheck.Tests;

/// <summary>
/// The JPEG seam. ImageSharp encodes as well as decodes, so the decoder is exercised on a
/// real JPEG built in the test rather than on a byte constant nobody can read - and the
/// same round trip is what gives the window tests a preview without a raw file.
/// </summary>
public class PreviewReaderTests
{
    static (byte, byte, byte) Flat(int x, int y) => (128, 160, 200);

    static double Median(PreviewImage p, int c)
    {
        var v = new List<double>(p.W * p.H);
        for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
                v.Add(p.At(y, x, c));
        return Stats.Percentile(v, 50);
    }

    [Fact]
    public void ARoundTrippedFlatComesBackAtItsLevel()
    {
        PreviewImage p = PreviewReader.Decode(TestImages.JpegOf(64, 48, Flat));

        Assert.NotNull(p);
        Assert.Equal(64, p.W);
        Assert.Equal(48, p.H);
        Assert.Equal(64 * 48 * 3, p.Rgb.Length);
        Assert.InRange(Median(p, 0), 127, 129);
        Assert.InRange(Median(p, 1), 159, 161);
        Assert.InRange(Median(p, 2), 199, 201);
    }

    /// <summary>
    /// The property AnalyzePreview leans on: levels keep their order through the codec.
    /// </summary>
    [Fact]
    public void AGradientKeepsItsOrder()
    {
        PreviewImage p = PreviewReader.Decode(TestImages.JpegOf(96, 64, (x, y) =>
        {
            byte v = (byte)(40 + 180 * x / 96);
            return (v, v, v);
        }));

        Assert.NotNull(p);
        Assert.True(p.At(32, 8, 0) < p.At(32, 48, 0));
        Assert.True(p.At(32, 48, 0) < p.At(32, 88, 0));
    }

    [Fact]
    public void ABitmapPreviewPassesThroughUntouched()
    {
        EmbeddedPreview e = TestImages.RgbOf(8, 6, Flat);

        PreviewImage p = PreviewReader.Decode(e);

        Assert.NotNull(p);
        Assert.Same(e.Data, p.Rgb);          // not even copied: no reason to
        Assert.Equal(e.Source, p.Source);
    }

    [Fact]
    public void ABitmapWithTheWrongLengthIsRefused()
    {
        var e = new EmbeddedPreview(false, new byte[10], 8, 6, 0, "x");

        Assert.Null(PreviewReader.Decode(e));
    }

    /// <summary>
    /// A preview that will not decode is not an error worth failing a run over; it just
    /// means the window's switch stays off. So: null, never an exception.
    /// </summary>
    [Fact]
    public void DecodeRefusesGarbageInsteadOfThrowing()
    {
        var rnd = new Random(7);
        var junk = new byte[4096];
        rnd.NextBytes(junk);

        Assert.Null(PreviewReader.Decode(new EmbeddedPreview(true, junk, 100, 100, 0, "x")));
        Assert.Null(PreviewReader.Decode(new EmbeddedPreview(true, Array.Empty<byte>(), 1, 1, 0, "x")));
        Assert.Null(PreviewReader.Decode(null));

        byte[] good = TestImages.JpegOf(64, 48, Flat).Data;
        byte[] cut = good.Take(good.Length / 2).ToArray();
        Assert.Null(PreviewReader.Decode(new EmbeddedPreview(true, cut, 64, 48, 0, "x")));
    }

    /// <summary>
    /// A preview whose long side is far above the target is decoded scaled, during IDCT,
    /// rather than at full size and discarded. The medians survive the scaling.
    /// </summary>
    [Fact]
    public void AnOversizedPreviewIsDecodedScaled()
    {
        int w = PreviewReader.TargetLongSide * 2, h = PreviewReader.TargetLongSide;
        PreviewImage p = PreviewReader.Decode(TestImages.JpegOf(w, h, Flat, quality: 70));

        Assert.NotNull(p);
        Assert.True(p.W <= PreviewReader.TargetLongSide + 8, $"{p.W}x{p.H}");
        Assert.True(p.W < w);
        Assert.InRange(Median(p, 2), 197, 203);
    }

    [Fact]
    public void AMidSizedPreviewIsDecodedAtNativeSize()
    {
        PreviewImage p = PreviewReader.Decode(TestImages.JpegOf(1600, 1000, Flat, quality: 70));

        Assert.NotNull(p);
        Assert.Equal(1600, p.W);
        Assert.Equal(1000, p.H);
    }

    /// <summary>
    /// The mirror of RawReaderTests.HoldsNoLibrawTypedFields: a field of a package type is
    /// resolved when the type loads, and this type is touched by the exposure path only
    /// when a preview exists. Keep the package reachable from method bodies alone.
    /// </summary>
    [Fact]
    public void HoldsNoImageSharpTypedFields()
    {
        var fields = typeof(PreviewReader).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var f in fields)
        {
            string asm = f.FieldType.Assembly.GetName().Name ?? "";
            Assert.False(asm.StartsWith("SixLabors", StringComparison.Ordinal),
                $"campul {f.Name} e de tip {f.FieldType.FullName} din {asm}");
        }
    }
}
