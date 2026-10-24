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

using SixLabors.ImageSharp.Formats.Tiff.Constants;

namespace AstroPhotoAnalyzer.Tests;

public class ImageSharpReaderTests
{
    /// <summary>
    /// A raster with distinct values on every pixel and channel, so that an index error shows.
    /// </summary>
    static Raster Pattern(int h = 6, int w = 8)
        => SyntheticRasters.From(h, w, 3, (x, y, p) => ((y * w + x) * 3 + p) * 997 % 65536 / 65535f);

    static void AssertSamePixels(Raster expected, Raster actual, int tolerance16 = 0)
    {
        Assert.Equal(expected.W, actual.W);
        Assert.Equal(expected.H, actual.H);
        Assert.Equal(expected.Planes.Length, actual.Planes.Length);
        for (int p = 0; p < expected.Planes.Length; p++)
            for (int i = 0; i < expected.Planes[p].Length; i++)
            {
                int e = RasterOps.Level16(expected.Planes[p][i]);
                int a = RasterOps.Level16(actual.Planes[p][i]);
                Assert.True(Math.Abs(e - a) <= tolerance16, $"plane {p}, pixel {i}: expected {e}, read {a}");
            }
    }

    /// <summary>
    /// The formula on 16-bit levels, the same as Pattern() but in integers, for the files
    /// written by hand.
    /// </summary>
    static int Level(int x, int y, int c, int w = 8) => ((y * w + x) * 3 + c) * 997 % 65536;

    [Fact]
    public void AHandBuiltTiff16RgbIsReadLevelForLevel()
    {
        using var ws = new TempWorkspace();
        Raster src = Pattern();
        string path = ws.WriteFile("p.tif", TiffTestData.Build16Rgb(8, 6, (x, y, c) => Level(x, y, c)));

        Raster r = new ImageSharpReader().Load(path);

        AssertSamePixels(src, r);
        Assert.Equal("TIFF", r.Source.Format);
        Assert.Equal(16, r.Source.BitDepth);
        Assert.Equal("necomprimat", r.Source.Compression);
        Assert.Equal(3, r.Source.PlanesInFile);
        Assert.False(r.Source.IsFloat);
        Assert.Null(r.Source.Icc);
        Assert.Null(r.Source.IccDescription);
        Assert.Equal(new FileInfo(path).Length, r.Source.FileBytes);
    }

    [Fact]
    public void Tiff8RgbLzwViaImageSharpReports8BitsAndScalesBy255()
    {
        using var ws = new TempWorkspace();
        Raster src = SyntheticRasters.From(2, 4, 3, (x, y, p) => ((y * 4 + x) * 3 + p) * 9 / 255f);
        string path = ImageFiles.SaveTiff8Rgb(src, ws.PathTo("p8.tif"));

        Raster r = new ImageSharpReader().Load(path);

        Assert.Equal(8, r.Source.BitDepth);
        Assert.Equal("LZW", r.Source.Compression);
        for (int p = 0; p < 3; p++)
            for (int i = 0; i < 8; i++)
                Assert.Equal((i * 3 + p) * 9, RasterOps.Level8(r.Planes[p][i]));
    }

    [Fact]
    public void Tiff16GrayIsReadAsOnePlane()
    {
        using var ws = new TempWorkspace();
        Raster src = SyntheticRasters.Ramp(4, 16, 0f, 1f, planes: 1);
        string path = ImageFiles.SaveTiff16Gray(src, ws.PathTo("g.tif"));

        Raster r = new ImageSharpReader().Load(path);

        AssertSamePixels(src, r);
        Assert.False(r.IsColor);
        Assert.Equal(1, r.Source.PlanesInFile);
        Assert.Equal(16, r.Source.BitDepth);
        Assert.Equal("Deflate", r.Source.Compression);
    }

    [Fact]
    public void Png8GrayReports8BitsAndScalesBy255()
    {
        using var ws = new TempWorkspace();
        Raster src = SyntheticRasters.From(2, 4, 1, (x, y, p) => (y * 4 + x) * 30 / 255f);
        string path = ImageFiles.SavePng8Gray(src, ws.PathTo("g.png"));

        Raster r = new ImageSharpReader().Load(path);

        Assert.Equal("PNG", r.Source.Format);
        Assert.Equal(8, r.Source.BitDepth);
        Assert.False(r.IsColor);
        // ImageSharp delivers 16 bits scaled by 257; divided by 65535 it gives exactly n/255.
        for (int i = 0; i < 8; i++)
            Assert.Equal(i * 30, RasterOps.Level8(r.Planes[0][i]));
    }

    [Fact]
    public void Png16RgbRoundTripsExactly()
    {
        using var ws = new TempWorkspace();
        Raster src = Pattern();
        string path = ImageFiles.SavePng16Rgb(src, ws.PathTo("p.png"));

        Raster r = new ImageSharpReader().Load(path);

        AssertSamePixels(src, r);
        Assert.Equal(16, r.Source.BitDepth);
        Assert.Equal("Deflate", r.Source.Compression);
    }

    [Fact]
    public void JpegIs8BitAndLossy()
    {
        using var ws = new TempWorkspace();
        Raster src = SyntheticRasters.Flat(16, 16, 0.3f, 0.5f, 0.7f);
        string path = ImageFiles.SaveJpeg(src, ws.PathTo("j.jpg"), quality: 95);

        Raster r = new ImageSharpReader().Load(path);

        Assert.Equal("JPEG", r.Source.Format);
        Assert.Equal(8, r.Source.BitDepth);
        Assert.Contains("JPEG", r.Source.Compression);
        Assert.True(r.IsColor);
        // On a uniform surface the JPEG is almost exact: under 2 levels of 255.
        AssertSamePixels(src, r, tolerance16: 2 * 257);
    }

    [Fact]
    public void AnIccProfileInATiffIsReadAndNamed()
    {
        using var ws = new TempWorkspace();
        byte[] icc = IccTestData.V2("Test sRGB profile");
        string path = ws.WriteFile("icc.tif", TiffTestData.Build16Rgb(8, 6, (x, y, c) => Level(x, y, c), icc));

        Raster r = new ImageSharpReader().Load(path);

        Assert.NotNull(r.Source.Icc);
        Assert.Equal("Test sRGB profile", r.Source.IccDescription);
        AssertSamePixels(Pattern(), r);   // the profile does not change the pixels
    }

    /// <summary>
    /// The shape of the acceptance image: a 16-bit PNG with iCCP. The PNG decoder must give
    /// both the profile and the levels, untouched by it.
    /// </summary>
    [Fact]
    public void AHandBuiltPng16WithIccpIsReadWithItsProfileAndLevels()
    {
        using var ws = new TempWorkspace();
        byte[] icc = IccTestData.V2("sRGB-elle-V2-srgbtrc.icc");
        string path = ws.WriteFile("icc.png", PngTestData.Build16Rgb(8, 6, (x, y, c) => Level(x, y, c), icc));

        Raster r = new ImageSharpReader().Load(path);

        Assert.Equal("PNG", r.Source.Format);
        Assert.Equal(16, r.Source.BitDepth);
        Assert.Equal("sRGB-elle-V2-srgbtrc.icc", r.Source.IccDescription);
        AssertSamePixels(Pattern(), r);
    }

    [Fact]
    public void AnUncompressedImageSharpTiffSaysSo()
    {
        using var ws = new TempWorkspace();
        string path = ImageFiles.SaveTiff8Rgb(Pattern(), ws.PathTo("u.tif"), TiffCompression.None);

        Raster r = new ImageSharpReader().Load(path);

        Assert.Equal("necomprimat", r.Source.Compression);
    }

    [Fact]
    public void AFileThatIsNotAnImageIsAnError()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("nu.tif", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var e = Assert.Throws<Exception>(() => new ImageSharpReader().Load(path));
        Assert.True(e.Message.Contains("imagine"), e.Message);
    }

    [Fact]
    public void HandlesTiffPngAndJpegByExtension()
    {
        var r = new ImageSharpReader();

        Assert.True(r.Handles("a.tif"));
        Assert.True(r.Handles("a.TIFF"));
        Assert.True(r.Handles("a.png"));
        Assert.True(r.Handles("a.jpg"));
        Assert.True(r.Handles("a.jpeg"));
        Assert.False(r.Handles("a.fit"));
        Assert.False(r.Handles("a.psd"));
    }
}
