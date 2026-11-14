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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// R-180 to R-182: the crop for a frame (ADR 0093). The rectangle of the frame's proportions,
/// its moves and its figures; the save, which writes the input's own kind of file and, for a
/// FITS, the input's own bytes; the proposed name.
/// </summary>
public class FrameCropTests
{
    const double FrameW = 70, FrameH = 54.62;

    // ------------------------------------------------------------------ R-180: the rectangle

    [Fact]
    public void R180_TheRectangleStartsAsTheLargestCentredOneWithTheSizesAsTyped()
    {
        // The reference image's size: the frame is narrower, so the width is cut, the odd pixel
        // on the right (R-032's rule).
        CropRect r = FrameCrop.Largest(5938, 3888, FrameW, FrameH);
        Assert.Equal(new CropRect(477, 0, 4983, 3888), r);
        Assert.InRange(Math.Abs(r.W - r.H * FrameW / FrameH), 0, 0.5);

        // The same two numbers the other way round: taller than wide, the full height kept.
        CropRect tall = FrameCrop.Largest(5938, 3888, FrameH, FrameW);
        Assert.Equal(3888, tall.H);
        Assert.Equal((int)Math.Round(3888 * FrameH / FrameW), tall.W);
        Assert.True(tall.H > tall.W);
        Assert.Equal((5938 - tall.W) / 2, tall.X);

        // A frame wider than the image cuts the height instead.
        CropRect wide = FrameCrop.Largest(4000, 3000, 100, 50);
        Assert.Equal(new CropRect(0, 500, 4000, 2000), wide);

        // The image's own proportions: the whole image.
        Assert.Equal(new CropRect(0, 0, 3000, 2000), FrameCrop.Largest(3000, 2000, 30, 20));
    }

    [Fact]
    public void R180_AMoveStopsAtTheImagesEdges()
    {
        var r = new CropRect(100, 50, 400, 300);
        Assert.Equal(new CropRect(130, 30, 400, 300), FrameCrop.Move(r, 30, -20, 1000, 800));
        Assert.Equal(new CropRect(0, 0, 400, 300), FrameCrop.Move(r, -5000, -5000, 1000, 800));
        Assert.Equal(new CropRect(600, 500, 400, 300), FrameCrop.Move(r, 5000, 5000, 1000, 800));
        // A rectangle as wide as the image moves only up and down.
        Assert.Equal(new CropRect(0, 60, 1000, 300), FrameCrop.Move(new CropRect(0, 50, 1000, 300), 77, 10, 1000, 800));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void R180_AResizeKeepsTheOppositeCornerAndTheProportions(int which)
    {
        // The four corners, by number: a public test cannot take the core's internal type.
        var corner = (CropCorner)which;
        Assert.True(Enum.IsDefined(corner));
        const int imageW = 2000, imageH = 1500;
        const double fw = 3, fh = 2;
        var r = new CropRect(500, 400, 600, 400);
        bool left = corner is CropCorner.TopLeft or CropCorner.BottomLeft, top = corner is CropCorner.TopLeft or CropCorner.TopRight;
        int anchorX = left ? r.X + r.W : r.X, anchorY = top ? r.Y + r.H : r.Y;

        // Pulled 150 px outward along x, the pointer staying level with the corner: 750 x 500.
        double cornerX = left ? r.X : r.X + r.W, cornerY = top ? r.Y : r.Y + r.H;
        CropRect grown = FrameCrop.Resize(r, corner, cornerX + (left ? -150 : 150), cornerY, imageW, imageH, fw, fh);
        Assert.Equal(750, grown.W);
        Assert.Equal(500, grown.H);
        Assert.Equal(anchorX, left ? grown.X + grown.W : grown.X);
        Assert.Equal(anchorY, top ? grown.Y + grown.H : grown.Y);

        // Pushed inward on both axes: the larger of the two pulls decides (x asks 300, y asks
        // 100 x 1.5 = 150), the opposite corner still where it was.
        CropRect shrunk = FrameCrop.Resize(r, corner, anchorX + (left ? -300 : 300), anchorY + (top ? -100 : 100), imageW, imageH, fw, fh);
        Assert.Equal(300, shrunk.W);
        Assert.Equal(200, shrunk.H);
        Assert.Equal(anchorX, left ? shrunk.X + shrunk.W : shrunk.X);
        Assert.Equal(anchorY, top ? shrunk.Y + shrunk.H : shrunk.Y);

        // Pulled far past the image: it stops at the first edge it meets, proportions kept.
        CropRect most = FrameCrop.Resize(r, corner, left ? -9000 : 9000, top ? -9000 : 9000, imageW, imageH, fw, fh);
        Assert.True(most.X >= 0 && most.Y >= 0 && most.X + most.W <= imageW && most.Y + most.H <= imageH, most.ToString());
        Assert.InRange(Math.Abs(most.W - most.H * fw / fh), 0, 0.75);
        Assert.True(most.X == 0 || most.Y == 0 || most.X + most.W == imageW || most.Y + most.H == imageH, most.ToString());
        Assert.True(most.W > r.W);
        Assert.Equal(anchorX, left ? most.X + most.W : most.X);
        Assert.Equal(anchorY, top ? most.Y + most.H : most.Y);

        // Pushed through the opposite corner: it stops at the smallest allowed, never turned
        // inside out.
        CropRect least = FrameCrop.Resize(r, corner, left ? 9000 : -9000, top ? 9000 : -9000, imageW, imageH, fw, fh);
        Assert.Equal(Thresholds.CropMinSide, Math.Min(least.W, least.H));
        Assert.Equal(96, least.W);
        Assert.Equal(anchorX, left ? least.X + least.W : least.X);
        Assert.Equal(anchorY, top ? least.Y + least.H : least.Y);
    }

    [Theory]
    [InlineData(FrameW, FrameH)]   // wider than tall, an awkward ratio
    [InlineData(FrameH, FrameW)]   // taller than wide: the short side is the width
    [InlineData(37, 10)]           // wider than the image: the room's width decides the largest
    public void R180_AResizeHoldsTheFramesProportionsWithinHalfAPixel(double fw, double fh)
    {
        // Every width, one pixel at a time, from under the smallest to past the largest the
        // image allows, pulled from two opposite corners.
        CropRect r = FrameCrop.Largest(1200, 900, fw, fh);
        var widths = new HashSet<int>();
        for (int pull = -5; pull < 1300; pull++)
            foreach (CropCorner corner in new[] { CropCorner.BottomRight, CropCorner.TopLeft })
            {
                bool far = corner == CropCorner.BottomRight;
                CropRect q = FrameCrop.Resize(r, corner, far ? r.X + pull : r.Right - pull, far ? r.Y + 1 : r.Bottom - 1, 1200, 900, fw, fh);
                double byWidth = Math.Abs(q.H - q.W * fh / fw), byHeight = Math.Abs(q.W - q.H * fw / fh);
                Assert.True(Math.Min(byWidth, byHeight) <= 0.5, $"{q} is off the frame's proportions");
                Assert.True(q.FitsIn(1200, 900), q.ToString());
                Assert.True(Math.Min(q.W, q.H) >= Thresholds.CropMinSide, q.ToString());
                Assert.Equal(far ? r.X : r.Right, far ? q.X : q.Right);
                Assert.Equal(far ? r.Y : r.Bottom, far ? q.Y : q.Bottom);
                widths.Add(q.W);
            }
        // The smallest and the largest were both reached: the short side at the minimum, and
        // the rectangle the image's largest.
        Assert.Contains(r.W, widths);
        Assert.Contains((int)Math.Ceiling(fw >= fh ? Thresholds.CropMinSide * fw / fh : Thresholds.CropMinSide), widths);
    }

    [Theory]
    [InlineData(40000000.0, 1.0)]
    [InlineData(1e300, 1e-300)]
    [InlineData(1.0, 1000000.0)]
    [InlineData(1e-300, 1e300)]
    public void R180_AFrameOfAbsurdProportionsStillGivesARectangleInsideTheImage(double fw, double fh)
    {
        const int w = 6000, h = 4000;
        CropRect r = FrameCrop.Largest(w, h, fw, fh);
        Assert.True(r.FitsIn(w, h), r.ToString());
        foreach (CropCorner corner in Enum.GetValues<CropCorner>())
            foreach ((double x, double y) in new[] { (-1e9, -1e9), (1e9, 1e9), (r.X, r.Y), (r.Right, r.Bottom), (3000.0, 2000.0), (double.NaN, 5.0) })
            {
                CropRect q = FrameCrop.Resize(r, corner, x, y, w, h, fw, fh);
                Assert.True(q.FitsIn(w, h), $"{corner} to ({x}, {y}) gave {q}");
                // The frame's long side stays the rectangle's long side.
                Assert.True(fw > fh ? q.W >= q.H : q.H >= q.W, q.ToString());
            }
    }

    [Fact]
    public void R180_AnImageSmallerThanTheMinimumStillHasItsOneRectangle()
    {
        // An image smaller than the minimum still has its one rectangle.
        CropRect tiny = FrameCrop.Largest(40, 30, 1, 1);
        Assert.Equal(new CropRect(5, 0, 30, 30), tiny);
        Assert.Equal(tiny, FrameCrop.Resize(tiny, CropCorner.BottomRight, 0, 0, 40, 30, 1, 1));
    }

    [Fact]
    public void R180_TheFiguresAreTheCutOnEachSideTheShareKeptAndThePixelsPerInch()
    {
        FrameCropFacts f = FrameCrop.Facts(FrameCrop.Largest(5938, 3888, FrameW, FrameH), 5938, 3888, FrameW);
        Assert.Equal((4983, 3888), (f.W, f.H));
        Assert.Equal((477, 478, 0, 0), (f.CutLeft, f.CutRight, f.CutTop, f.CutBottom));
        Assert.Equal(100.0 * 4983 / 5938, f.PctKept, 9);
        Assert.Equal(4983 / (70 / 2.54), f.Ppi, 9);

        FrameCropFacts g = FrameCrop.Facts(new CropRect(100, 50, 400, 300), 1000, 800, 20);
        Assert.Equal((100, 500, 50, 450), (g.CutLeft, g.CutRight, g.CutTop, g.CutBottom));
        Assert.Equal(15.0, g.PctKept, 9);

        // The sentences, in plain words, the numbers the core's.
        Assert.Equal("ramane 4983 x 3888 px, 83.9 % din poza; se taie 477 px in stanga si 478 px in dreapta", CropTexts.Kept(f));
        Assert.Equal("pe o rama de 70 x 54.62 cm: 181 pixeli pe inch (ppi)", CropTexts.AtFrame(f, FrameW, FrameH));
        Assert.Equal("ramane 400 x 300 px, 15.0 % din poza; se taie 100 px in stanga, 500 px in dreapta, 50 px sus si 450 px jos", CropTexts.Kept(g));
        Assert.Equal("ramane 3000 x 2000 px, 100.0 % din poza; nu se taie nimic", CropTexts.Kept(FrameCrop.Facts(new CropRect(0, 0, 3000, 2000), 3000, 2000, 30)));
    }

    [Theory]
    [InlineData("70", 70.0)]
    [InlineData("54.62", 54.62)]
    [InlineData(" 40.0 ", 40.0)]
    public void R180_ASizeIsANumberAboveZeroWrittenWithAPoint(string typed, double cm)
        => Assert.Equal(cm, FrameCrop.ParseSize(typed));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("70,5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e400")]
    public void R180_ASizeThatIsNotANumberAboveZeroGivesNoRectangle(string typed)
    {
        Assert.Null(FrameCrop.ParseSize(typed));
        Assert.Contains("Latimea ramei", CropTexts.BadSize(width: true, typed));
        Assert.Contains("Inaltimea ramei", CropTexts.BadSize(width: false, typed));
        Assert.Contains("mai mare ca 0", CropTexts.BadSize(width: true, typed));
    }

    // ------------------------------------------------------------------ R-181: the save

    /// <summary>The length of a FITS HDU's header: up to and with the block that holds END.</summary>
    static int HeaderLength(byte[] file, int from = 0)
    {
        for (int i = from; i + 80 <= file.Length; i += 80)
            if (Encoding.ASCII.GetString(file, i, 8) == "END     ") return (i - from) / 2880 * 2880 + 2880;
        throw new Exception("no END card");
    }

    static string[] Cards(byte[] file, int from = 0)
    {
        var cards = new List<string>();
        for (int i = from; i + 80 <= file.Length; i += 80)
        {
            string c = Encoding.ASCII.GetString(file, i, 80);
            cards.Add(c);
            if (c.StartsWith("END     ")) break;
        }
        return cards.ToArray();
    }

    /// <summary>
    /// The bytes a crop of a FITS must hold: for each plane, the file's rows that are the
    /// raster's rows of the rectangle (the raster's row 0 is the file's last), the rectangle's
    /// columns of each.
    /// </summary>
    static byte[] RectangleBytes(byte[] source, int dataAt, int w, int h, int planes, int bytesPer, CropRect r)
    {
        var o = new List<byte>();
        for (int p = 0; p < planes; p++)
            for (int fileRow = h - (r.Y + r.H); fileRow < h - r.Y; fileRow++)
                o.AddRange(source.AsSpan(dataAt + ((p * h + fileRow) * w + r.X) * bytesPer, r.W * bytesPer).ToArray());
        return o.ToArray();
    }

    static Raster Cropped(Raster r, CropRect c)
    {
        var o = new Raster(c.H, c.W, r.Planes.Length);
        for (int p = 0; p < r.Planes.Length; p++)
            for (int y = 0; y < c.H; y++)
                Array.Copy(r.Planes[p], (c.Y + y) * r.W + c.X, o.Planes[p], y * c.W, c.W);
        return o;
    }

    static void AssertSamePixels(Raster want, Raster got)
    {
        Assert.Equal((want.W, want.H, want.Planes.Length), (got.W, got.H, got.Planes.Length));
        for (int p = 0; p < want.Planes.Length; p++) Assert.Equal(want.Planes[p], got.Planes[p]);
    }

    /// <summary>A physical value for every BITPIX, different at every sample, with digits a float raster cannot hold.</summary>
    static double Sample(int bitpix, int x, int y, int p) => bitpix switch
    {
        8 => 60 + (x * 7 + y * 13 + p * 29) % 180,
        16 => 9000 + (x * 251 + y * 1013 + p * 7919) % 52000,
        // Odd 32-bit values: their low bits are lost in a 24-bit mantissa.
        32 => 1_000_000_001.0 + x * 65_537.0 + y * 1_000_003.0 + p * 7,
        // Reals outside 0..1 at two pixels, which the reader clips.
        -32 => (x == 6 && y == 12) ? 1.75 : (x == 7 && y == 12) ? -0.25 : 0.2 + 0.004 * x + 0.011 * y + 0.05 * p,
        _ => 0.25 + x * 1e-11 + y * 1e-3 + p * 0.07,
    };

    [Theory]
    [InlineData(8, 1)]
    [InlineData(16, 3)]
    [InlineData(32, 1)]
    [InlineData(-32, 3)]
    [InlineData(-64, 1)]
    public void R181_AFitsCropHoldsTheInputsOwnBytesAtItsOwnBitpix(int bitpix, int planes)
    {
        using var ws = new TempWorkspace();
        const int w = 40, h = 30;
        byte[] source = FitsTestData.Build(w, h, planes, (x, y, p) => Sample(bitpix, x, y, p), bitpix: bitpix,
            extraCards: new[] { FitsTestData.StringCard("OBJECT", "M8") });
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var rect = new CropRect(5, 3, 20, 16);
        string target = ws.PathTo("in_cropped_70x54.62.fit");

        CropExport.Save(a, rect, FrameW, FrameH, target);

        Assert.Equal(source, File.ReadAllBytes(input));   // the input is byte for byte what it was
        byte[] saved = File.ReadAllBytes(target);
        Assert.Equal(0, saved.Length % 2880);
        int bytesPer = Math.Abs(bitpix) / 8;
        byte[] want = RectangleBytes(source, HeaderLength(source), w, h, planes, bytesPer, rect);
        int dataAt = HeaderLength(saved);
        Assert.Equal(want, saved[dataAt..(dataAt + want.Length)]);
        Assert.All(saved[(dataAt + want.Length)..], b => Assert.Equal(0, b));   // the padding of the last block

        var hdr = FitsReader.ReadImageHeader(target);
        Assert.Equal(bitpix, FitsReader.HInt(hdr.Header, "BITPIX", 0));
        Assert.Equal((20, 16), (hdr.W, hdr.H));
        Assert.Equal("M8", FitsReader.HStr(hdr.Header, "OBJECT", null));

        // Read back by the tool: the rectangle of the analysed raster, sample for sample.
        Raster back = ImageLoader.Load(target);
        AssertSamePixels(Cropped(a.Raster, rect), back);
        Assert.Equal(a.Raster.Source.BitDepth, back.Source.BitDepth);
        Assert.Equal(a.Raster.Source.IsFloat, back.Source.IsFloat);
    }

    static string CommentedCard(string key, string value, string comment) => (key.PadRight(8) + "= " + value.PadLeft(20) + " / " + comment).PadRight(80);

    [Fact]
    public void R181_AFitsCropKeepsEveryCardAndMovesTheSizeAndTheReferencePixel()
    {
        using var ws = new TempWorkspace();
        string[] extra =
        {
            FitsTestData.StringCard("ROWORDER", "BOTTOM-UP"), FitsTestData.StringCard("INSTRUME", "Nikon D3300", "Instrument name"),
            FitsTestData.StringCard("CTYPE1", "RA---TAN"), FitsTestData.StringCard("CTYPE2", "DEC--TAN"),
            CommentedCard("CRPIX1", "32.5", "Axis1 reference pixel"), CommentedCard("CRPIX2", "32.5", "Axis2 reference pixel"),
            FitsTestData.Card("CRVAL1", "281.4"), FitsTestData.Card("CRVAL2", "-1.95"),
            FitsTestData.Card("CD1_1", "-0.06"), FitsTestData.Card("CD1_2", "0.004"), FitsTestData.Card("CD2_1", "0.004"), FitsTestData.Card("CD2_2", "0.06"),
            FitsTestData.Card("EXPTIME", "119.2"),
        };
        byte[] source = FitsTestData.Build(64, 64, 3, (x, y, p) => 0.05 + 0.9 * (x + 64 * y) / 4096.0 * (p + 1) / 3, bitpix: -32, extraCards: extra);
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var rect = new CropRect(10, 6, 40, 30);
        string target = ws.PathTo("out.fit");

        CropExport.Save(a, rect, FrameW, FrameH, target);

        string[] before = Cards(source), after = Cards(File.ReadAllBytes(target));
        // Every card of the source in its place; four of them with a new value and their own
        // comment; then HISTORY; then END.
        for (int i = 0; i < before.Length - 1; i++)
        {
            string key = before[i][..8].TrimEnd();
            if (key is "NAXIS1" or "NAXIS2" or "CRPIX1" or "CRPIX2") Assert.StartsWith(key, after[i]);
            else Assert.Equal(before[i], after[i]);
        }
        string Card(string key) => after.Single(c => c[..8].TrimEnd() == key);
        Assert.Equal(FitsTestData.Card("NAXIS1", "40"), Card("NAXIS1"));
        Assert.Equal(FitsTestData.Card("NAXIS2", "30"), Card("NAXIS2"));
        // 10 columns cut on the left; the file's rows cut before the first kept one are the
        // raster's rows under the rectangle: 64 - (6 + 30) = 28.
        Assert.Equal(CommentedCard("CRPIX1", "22.5", "Axis1 reference pixel"), Card("CRPIX1"));
        Assert.Equal(CommentedCard("CRPIX2", "4.5", "Axis2 reference pixel"), Card("CRPIX2"));
        string[] added = after[(before.Length - 1)..^1];
        Assert.NotEmpty(added);
        Assert.All(added, card => Assert.StartsWith("HISTORY ", card));
        string history = string.Concat(added.Select(card => card[8..].TrimEnd() + " "));
        Assert.Contains("AstroPhotoAnalyzer", history);
        Assert.Contains("70 x 54.62", history);
        Assert.Contains("40 x 30", history);

        // The plate solution names the same sky for the same pixel before and after.
        Raster back = ImageLoader.Load(target);
        Wcs was = a.Raster.Source.Wcs, now = back.Source.Wcs;
        Assert.NotNull(was);
        Assert.NotNull(now);
        foreach ((int x, int y) in new[] { (0, 0), (39, 29), (17, 4) })
        {
            var (ra0, dec0) = was.PixelToSky(rect.X + x, rect.Y + y);
            var (ra1, dec1) = now.PixelToSky(x, y);
            Assert.Equal(ra0, ra1, 9);
            Assert.Equal(dec0, dec1, 9);
        }
    }

    [Fact]
    public void R181_AFitsWithoutAPlateSolutionGainsNoReferencePixelAndTheHdusAroundAreCopied()
    {
        using var ws = new TempWorkspace();
        // TOP-DOWN in the header: the reader flips the rows all the same (ADR 0049), and the
        // crop copies the file's rows in the file's own order.
        byte[] image = FitsTestData.Build(40, 24, 1, (x, y, _) => 2000 + 1500 * ((x + 3 * y) % 36),
            extraCards: new[] { FitsTestData.StringCard("ROWORDER", "TOP-DOWN") });
        byte[] trailing = FitsTestData.Build2D(8, 8, (x, y) => 100 * (x + y));
        byte[] source = FitsTestData.WithEmptyPrimary(image).Concat(trailing).ToArray();
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var rect = new CropRect(8, 2, 24, 18);
        string target = ws.PathTo("out.fit");

        CropExport.Save(a, rect, 4, 3, target);

        byte[] saved = File.ReadAllBytes(target);
        Assert.Equal(source[..2880], saved[..2880]);          // the empty primary
        Assert.Equal(trailing, saved[^trailing.Length..]);   // the HDU after the image
        Assert.Equal(0, saved.Length % 2880);
        string[] cards = Cards(saved, 2880);
        Assert.DoesNotContain(cards, c => c.StartsWith("CRPIX"));
        Assert.Contains(FitsTestData.StringCard("ROWORDER", "TOP-DOWN"), cards);
        int srcData = 2880 + HeaderLength(source, 2880), dstData = 2880 + HeaderLength(saved, 2880);
        byte[] want = RectangleBytes(source, srcData, 40, 24, 1, 2, rect);
        Assert.Equal(want, saved[dstData..(dstData + want.Length)]);
        AssertSamePixels(Cropped(a.Raster, rect), ImageLoader.Load(target));
    }

    static Raster Scene(int h, int w, int planes)
        => SyntheticRasters.From(h, w, planes, (x, y, p) => (1000 + (x * 251 + y * 1013 + p * 7919) % 60000) / 65535f);

    [Fact]
    public void R181_ATiffIsWrittenAtTheInputsDepthWithItsProfile()
    {
        using var ws = new TempWorkspace();
        var rect = new CropRect(7, 5, 30, 22);
        byte[] icc = IccTestData.V2("sRGB test");

        // 16 bits, colour, with a profile.
        Raster s = Scene(48, 64, 3);
        string in16 = ws.WriteFile("a.tif", TiffTestData.Build16Rgb(s.W, s.H, (x, y, c) => RasterOps.Level16(s.Planes[c][y * s.W + x]), icc));
        AnalysisResult a16 = AnalysisRun.Execute(in16, Scenes.OnBuiltForPaper());
        string out16 = ws.PathTo("a_cropped.tif");
        CropExport.Save(a16, rect, FrameW, FrameH, out16);
        Raster b16 = ImageLoader.Load(out16);
        Assert.Equal(("TIFF", 16, 3), (b16.Source.Format, b16.Source.BitDepth, b16.Source.PlanesInFile));
        Assert.Equal(icc, b16.Source.Icc);
        AssertSamePixels(Cropped(a16.Raster, rect), b16);

        // 8 bits, colour, compressed, no profile: 8 bits again, and no profile is added.
        string in8 = ImageFiles.SaveTiff8Rgb(Scene(48, 64, 3), ws.PathTo("b.tif"));
        AnalysisResult a8 = AnalysisRun.Execute(in8, Scenes.OnBuiltForPaper());
        string out8 = ws.PathTo("b_cropped.tif");
        CropExport.Save(a8, rect, FrameW, FrameH, out8);
        Raster b8 = ImageLoader.Load(out8);
        Assert.Equal(("TIFF", 8, 3), (b8.Source.Format, b8.Source.BitDepth, b8.Source.PlanesInFile));
        Assert.Null(b8.Source.Icc);
        AssertSamePixels(Cropped(a8.Raster, rect), b8);

        // 16 bits, grey: grey again.
        string inGrey = ImageFiles.SaveTiff16Gray(Scene(48, 64, 1), ws.PathTo("c.tif"));
        AnalysisResult aGrey = AnalysisRun.Execute(inGrey, Scenes.OnBuiltForPaper());
        string outGrey = ws.PathTo("c_cropped.tif");
        CropExport.Save(aGrey, rect, FrameW, FrameH, outGrey);
        Raster bGrey = ImageLoader.Load(outGrey);
        Assert.Equal(("TIFF", 16, 1), (bGrey.Source.Format, bGrey.Source.BitDepth, bGrey.Source.PlanesInFile));
        AssertSamePixels(Cropped(aGrey.Raster, rect), bGrey);
    }

    [Fact]
    public void R181_APngIsWrittenAtSixteenBitsOrEightWithItsProfile()
    {
        using var ws = new TempWorkspace();
        var rect = new CropRect(7, 5, 30, 22);
        byte[] icc = IccTestData.V2("sRGB test");

        Raster s = Scene(48, 64, 3);
        string in16 = ws.WriteFile("a.png", PngTestData.Build16Rgb(s.W, s.H, (x, y, c) => RasterOps.Level16(s.Planes[c][y * s.W + x]), icc));
        AnalysisResult a16 = AnalysisRun.Execute(in16, Scenes.OnBuiltForPaper());
        string out16 = ws.PathTo("a_cropped.png");
        CropExport.Save(a16, rect, FrameW, FrameH, out16);
        Raster b16 = ImageLoader.Load(out16);
        Assert.Equal(("PNG", 16, 3), (b16.Source.Format, b16.Source.BitDepth, b16.Source.PlanesInFile));
        Assert.Equal(icc, b16.Source.Icc);
        AssertSamePixels(Cropped(a16.Raster, rect), b16);

        string in8 = ImageFiles.SavePng8Gray(Scene(48, 64, 1), ws.PathTo("b.png"));
        AnalysisResult a8 = AnalysisRun.Execute(in8, Scenes.OnBuiltForPaper());
        string out8 = ws.PathTo("b_cropped.png");
        CropExport.Save(a8, rect, FrameW, FrameH, out8);
        Raster b8 = ImageLoader.Load(out8);
        Assert.Equal(("PNG", 8, 1), (b8.Source.Format, b8.Source.BitDepth, b8.Source.PlanesInFile));
        Assert.Null(b8.Source.Icc);
        AssertSamePixels(Cropped(a8.Raster, rect), b8);
    }

    static (CropFileKind, int, string?) Kind(SourceInfo s)
    {
        CropOutput o = CropExport.OutputOf(s);
        return (o.Kind, o.Bits, o.Extension);
    }

    [Fact]
    public void R181_AJpegBecomesAnEightBitTiffAndATiffOfAnotherDepthIsRefused()
    {
        using var ws = new TempWorkspace();
        var rect = new CropRect(7, 5, 30, 22);
        string jpeg = ImageFiles.SaveJpeg(Scene(48, 64, 3), ws.PathTo("poza.jpg"));
        AnalysisResult a = AnalysisRun.Execute(jpeg, Scenes.OnBuiltForPaper());
        CropOutput kind = CropExport.OutputOf(a.Raster.Source);
        Assert.Equal((CropFileKind.Tiff, 8, ".tif"), (kind.Kind, kind.Bits, kind.Extension));
        Assert.Null(kind.Problem);
        Assert.Contains("JPEG", CropTexts.Output(kind));
        Assert.Contains("TIFF pe 8 biti", CropTexts.Output(kind));

        string target = ws.PathTo("poza_cropped_70x54.62.tif");
        CropExport.Save(a, rect, FrameW, FrameH, target);
        Raster back = ImageLoader.Load(target);
        Assert.Equal(("TIFF", 8, 3), (back.Source.Format, back.Source.BitDepth, back.Source.PlanesInFile));
        // The JPEG's decoded pixels, none changed by a second compression.
        AssertSamePixels(Cropped(a.Raster, rect), back);

        // The kinds, from what the reader says of the file.
        Assert.Equal((CropFileKind.Fits, 32, (string?)null), Kind(new SourceInfo { Format = "FITS", BitDepth = 32, IsFloat = true }));
        Assert.Equal((CropFileKind.Tiff, 16, (string?)null), Kind(new SourceInfo { Format = "TIFF", BitDepth = 16 }));
        Assert.Equal((CropFileKind.Png, 16, (string?)null), Kind(new SourceInfo { Format = "PNG", BitDepth = 16 }));
        Assert.Equal((CropFileKind.Png, 8, (string?)null), Kind(new SourceInfo { Format = "PNG", BitDepth = 4 }));
        Assert.Contains("FITS pe 32 de biti", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "FITS", BitDepth = 32, IsFloat = true })));
        Assert.Contains("TIFF pe 16 biti", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "TIFF", BitDepth = 16 })));

        // A TIFF the reader only gets through a conversion cannot be cropped without loss.
        Raster r = Scene(48, 64, 3);
        r.Source = new SourceInfo { Format = "TIFF", BitDepth = 32, PlanesInFile = 3 };
        CropOutput refused = CropExport.OutputOf(r.Source);
        Assert.Contains("un TIFF cu 32 de biti pe esantion nu poate fi decupat fara pierderi", refused.Problem);
        AnalysisResult inMemory = AnalysisRun.Execute(r, "memorie.tif", Scenes.OnBuiltForPaper());
        string no = ws.PathTo("no.tif");
        var e = Assert.Throws<Exception>(() => CropExport.Save(inMemory, rect, FrameW, FrameH, no));
        Assert.Contains("nu poate fi decupat fara pierderi", e.Message);
        Assert.False(File.Exists(no));
    }

    [Fact]
    public void R181_TheInputIsNeverWrittenAndARefusedSaveLeavesNoFile()
    {
        using var ws = new TempWorkspace();
        byte[] source = FitsTestData.Build(40, 30, 3, (x, y, p) => Sample(-32, x, y, p), bitpix: -32);
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var rect = new CropRect(5, 3, 20, 16);

        // Over the input, by its path.
        var e = Assert.Throws<Exception>(() => CropExport.Save(a, rect, FrameW, FrameH, input));
        Assert.Contains("imaginea de intrare", e.Message);
        Assert.Equal(source, File.ReadAllBytes(input));

        // A rectangle that is not inside the image is a caller's mistake, refused before any write.
        string target = ws.PathTo("out.fit");
        Assert.ThrowsAny<ArgumentException>(() => CropExport.Save(a, new CropRect(30, 3, 20, 16), FrameW, FrameH, target));
        Assert.False(File.Exists(target));

        // The input gone: refused, never a FITS without its header.
        File.Delete(input);
        e = Assert.Throws<Exception>(() => CropExport.Save(a, rect, FrameW, FrameH, target));
        Assert.Contains("nu mai e unde era", e.Message);
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(ws.Dir));

        // A raster from memory has no file to copy: a fresh 32-bit float FITS of the rectangle.
        AnalysisResult memory = AnalysisRun.Execute(Scene(48, 64, 3), "memorie", Scenes.OnBuiltForPaper());
        CropExport.Save(memory, rect, FrameW, FrameH, target);
        AssertSamePixels(Cropped(memory.Raster, rect), ImageLoader.Load(target));
    }

    [Fact]
    public void R181_AReferencePixelIsMovedExactlyHoweverItIsWritten()
    {
        using var ws = new TempWorkspace();
        string[] extra =
        {
            // 0.3 - 10 is -9.6999999999999993 in binary; the card must say -9.7 and keep its comment.
            CommentedCard("CRPIX1", "0.3", "Axis1 reference pixel"),
            // A value some writers quote: still a number, moved and written as one.
            FitsTestData.StringCard("CRPIX2", "32.5"),
            // An alternate solution's, with a FITS exponent.
            FitsTestData.Card("CRPIX1A", "1.5D1"),
        };
        byte[] source = FitsTestData.Build(64, 64, 1, (x, y, _) => 0.2 + 0.0001 * (x + 64 * y), bitpix: -32, extraCards: extra);
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        string target = ws.PathTo("out.fit");
        CropExport.Save(a, new CropRect(10, 6, 40, 30), FrameW, FrameH, target);

        string[] after = Cards(File.ReadAllBytes(target));
        Assert.Contains(CommentedCard("CRPIX1", "-9.7", "Axis1 reference pixel"), after);
        Assert.Contains(FitsTestData.Card("CRPIX2", "4.5"), after);   // 64 - (6 + 30) = 28 file rows cut before
        Assert.Contains(FitsTestData.Card("CRPIX1A", "5."), after);
    }

    [Fact]
    public void R181_AHeaderThatGrowsPastItsBlockAndASourceThatChangedSinceTheAnalysis()
    {
        using var ws = new TempWorkspace();
        // 5 cards of the image and 30 more: END is the 36th, the last of the block, so the
        // HISTORY cards of the crop open a second one.
        string[] extra = Enumerable.Range(0, 30).Select(i => FitsTestData.Card("KEY" + i.ToString("00"), i.ToString())).ToArray();
        byte[] source = FitsTestData.Build(40, 30, 1, (x, y, _) => Sample(-32, x, y, 0), bitpix: -32, extraCards: extra);
        Assert.Equal(2880, HeaderLength(source));
        string input = ws.WriteFile("in.fit", source);
        AnalysisResult a = AnalysisRun.Execute(input, Scenes.OnBuiltForPaper());
        var rect = new CropRect(5, 3, 20, 16);
        string target = ws.PathTo("out.fit");

        CropExport.Save(a, rect, FrameW, FrameH, target);
        byte[] saved = File.ReadAllBytes(target);
        Assert.Equal(5760, HeaderLength(saved));
        byte[] want = RectangleBytes(source, 2880, 40, 30, 1, 4, rect);
        Assert.Equal(want, saved[5760..(5760 + want.Length)]);
        AssertSamePixels(Cropped(a.Raster, rect), ImageLoader.Load(target));
        File.Delete(target);

        // The input cut short since the analysis: refused, nothing written.
        File.WriteAllBytes(input, source[..(source.Length - 2880)]);
        var e = Assert.Throws<Exception>(() => CropExport.Save(a, rect, FrameW, FrameH, target));
        Assert.Contains("trunchiat", e.Message);
        Assert.False(File.Exists(target));

        // Another image under the same name: refused by its geometry.
        File.WriteAllBytes(input, FitsTestData.Build(30, 40, 1, (x, y, _) => 0.3, bitpix: -32));
        e = Assert.Throws<Exception>(() => CropExport.Save(a, rect, FrameW, FrameH, target));
        Assert.Contains("geometria", e.Message);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void R181_TheOtherDepthsAndColoursOfATiffAndAPngKeepTheirKind()
    {
        using var ws = new TempWorkspace();
        var rect = new CropRect(7, 5, 30, 22);
        (string Path, string Format, int Bits, int Planes)[] cases =
        {
            (ImageFiles.SaveTiff8Gray(Scene(48, 64, 1), ws.PathTo("a.tif")), "TIFF", 8, 1),
            (ImageFiles.SavePng16Gray(Scene(48, 64, 1), ws.PathTo("b.png")), "PNG", 16, 1),
            (ImageFiles.SavePng8Rgb(Scene(48, 64, 3), ws.PathTo("c.png")), "PNG", 8, 3),
        };
        foreach (var c in cases)
        {
            AnalysisResult a = AnalysisRun.Execute(c.Path, Scenes.OnBuiltForPaper());
            Assert.Equal((c.Format, c.Bits, c.Planes), (a.Raster.Source.Format, a.Raster.Source.BitDepth, a.Raster.Source.PlanesInFile));
            string target = ws.PathTo("cropped_" + Path.GetFileName(c.Path));
            CropExport.Save(a, rect, FrameW, FrameH, target);
            Raster back = ImageLoader.Load(target);
            Assert.Equal((c.Format, c.Bits, c.Planes), (back.Source.Format, back.Source.BitDepth, back.Source.PlanesInFile));
            AssertSamePixels(Cropped(a.Raster, rect), back);
        }
    }

    [Fact]
    public void R181_TheSentencesOfTheOutputReadRightInEnglishAndAMisnamedInputGetsItsOwnExtension()
    {
        using IDisposable english = Tr.Use(UiLanguage.English);
        Assert.Contains("writes an 8-bit TIFF with the same pixels", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "JPEG", BitDepth = 8, Path = "poza.jpg" })));
        Assert.Contains(": an 8-bit PNG.", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "PNG", BitDepth = 8, Path = "a.png" })));
        Assert.Contains(": a 16-bit TIFF.", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "TIFF", BitDepth = 16, Path = "a.tif" })));
        Assert.Contains(": a 32-bit FITS (real numbers).", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "FITS", BitDepth = 32, IsFloat = true, Path = "a.fit" })));
        // A refusal is shown as a sentence of its own.
        Assert.StartsWith("A TIFF with 32 bits per sample cannot be cropped without loss", CropTexts.Output(CropExport.OutputOf(new SourceInfo { Format = "TIFF", BitDepth = 32, Path = "a.tif" })));

        // The file written follows the content the reader found, so its name does too.
        Assert.Equal(".png", CropExport.OutputOf(new SourceInfo { Format = "PNG", BitDepth = 16, Path = @"C:\x\poza.tif" }).Extension);
        Assert.Equal(".tif", CropExport.OutputOf(new SourceInfo { Format = "TIFF", BitDepth = 8, Path = "poza.png" }).Extension);
        Assert.Null(CropExport.OutputOf(new SourceInfo { Format = "TIFF", BitDepth = 8, Path = "poza.TIFF" }).Extension);
        Assert.Null(CropExport.OutputOf(new SourceInfo { Format = "FITS", BitDepth = 16, Path = "stack.fts" }).Extension);
        Assert.Equal("poza_cropped_70x54.62.png", CropTexts.ProposedFileName(@"C:\x\poza.tif", ".png", 70, 54.62));
    }

    // ------------------------------------------------------------------ R-182: the name

    [Fact]
    public void R182_TheProposedNameCarriesTheFrameAsTypedAndTheInputsKind()
    {
        Assert.Equal("M31_cropped_70x54.62.fit", CropTexts.ProposedFileName(@"C:\astro\M31.fit", null, 70, 54.62));
        Assert.Equal("poza_cropped_50x40.tif", CropTexts.ProposedFileName(@"C:\x\poza.jpg", ".tif", 50, 40.0));
        Assert.Equal("de_printat_cropped_54.62x70.png", CropTexts.ProposedFileName("de_printat.png", null, 54.62, 70));
        Assert.Equal("stack.v2_cropped_100x66.667.fits", CropTexts.ProposedFileName("stack.v2.fits", null, 100, 66.667));
        // As typed, whatever the number of decimals.
        Assert.Equal("a_cropped_54.62345x0.00125.tif", CropTexts.ProposedFileName("a.tif", null, 54.62345, 0.00125));
    }
}
