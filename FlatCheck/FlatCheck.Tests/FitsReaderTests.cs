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
/// Phase 3: the FITS reader. This is pure byte-shuffling with a lot of edge cases and
/// previously had zero coverage, so it is the highest-value target in the plan.
///
/// Everything here goes through the in-memory <c>Load(byte[])</c> overload; only the
/// path-based test touches disk.
/// </summary>
public class FitsReaderTests
{
    static Img Load(byte[] fits) => FitsReader.Load(fits);

    // ---------------------------------------------------------------- geometry

    [Fact]
    public void Load_ReadsDimensions_WithNaxis1AsWidthAndNaxis2AsHeight()
    {
        var img = Load(FitsTestData.BuildFits(7, 3, (x, y) => 100));

        Assert.Equal(7, img.W);
        Assert.Equal(3, img.H);
    }

    [Fact]
    public void Load_StoresPixelsRowMajor()
    {
        // value encodes its own coordinates so a transpose would be obvious; the builder writes
        // the file as Siril does and the reader undoes it, so the scene comes back as drawn
        var img = Load(FitsTestData.BuildFits(4, 3, (x, y) => 1000 + y * 10 + x));

        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                Assert.Equal(1000 + y * 10 + x, img.At(y, x), 6);
    }

    // ---------------------------------------------------------------- row order (R-130)

    /// <summary>
    /// Siril draws every FITS with the file's first row at the bottom, whatever ROWORDER says,
    /// and uses the keyword only for the Bayer phase (measured 2026-10-26 through siril-cli,
    /// ImageVisualCompare ADR 0039). The reader does the same, so row 0 of the raster is the top
    /// of the frame as Siril shows it, for the three states of the card alike (ADR 0094). The
    /// same through the path-based load, which every mode uses.
    /// </summary>
    [Theory]
    [InlineData("BOTTOM-UP")]
    [InlineData("TOP-DOWN")]
    [InlineData(null)]
    public void R130_TheFirstRowOfTheFile_IsTheRasterLastRow_WhateverRowOrderSays(string rowOrder)
    {
        string[]? cards = rowOrder == null ? null : new[] { FitsTestData.Card("ROWORDER", "'" + rowOrder + "'") };
        byte[] fits = FitsTestData.BuildFits(4, 3, (x, y) => 1000 + y * 10 + x, extraCards: cards, bottomUp: false);

        var img = Load(fits);

        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                Assert.Equal(1000 + (2 - y) * 10 + x, img.At(y, x), 6);

        using var ws = new TempWorkspace();
        Img loaded = ImageLoader.Load(ws.WriteFile("rows.fit", fits)).Image;
        Assert.Equal(1020, loaded.At(0, 0), 6);
        Assert.Equal(1000, loaded.At(2, 0), 6);
    }

    /// <summary>
    /// The same rule on a colour cube: the planes are averaged and the file's first row lands
    /// last, in the branch that reads several planes (R-130).
    /// </summary>
    [Fact]
    public void R130_ACubeIsAveragedOverItsPlanes_WithTheFilesFirstRowLast()
    {
        var img = Load(FitsTestData.BuildFitsCube(4, 3, 3, (x, y, p) => 1000 + y * 10 + x + p, bottomUp: false));

        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                Assert.Equal(1000 + (2 - y) * 10 + x + 1, img.At(y, x), 6);   // the mean of p = 0, 1, 2
    }

    // ---------------------------------------------------------------- BITPIX

    [Theory]
    [InlineData(8, 200.0)]
    [InlineData(16, 30000.0)]
    [InlineData(32, 1234567.0)]
    [InlineData(-32, 1234.5)]
    [InlineData(-64, 1234.5)]
    public void Load_RoundTripsEverySupportedBitpix(int bitpix, double value)
    {
        var img = Load(FitsTestData.BuildFits(4, 4, (x, y) => value, bitpix: bitpix));

        Assert.Equal(value, img.At(2, 2), 3);
    }

    [Fact]
    public void Load_ReadsSigned16BitNegatives_WhenBzeroIsZero()
    {
        var img = Load(FitsTestData.BuildFits(4, 4, (x, y) => -1234, bitpix: 16, bzero: 0));

        Assert.Equal(-1234, img.At(1, 1), 6);
    }

    [Fact]
    public void Load_ReadsSigned32BitNegatives()
    {
        var img = Load(FitsTestData.BuildFits(4, 4, (x, y) => -70000, bitpix: 32));

        Assert.Equal(-70000, img.At(1, 1), 6);
    }

    [Fact]
    public void Load_RejectsAnUnsupportedBitpix()
    {
        // BITPIX 64 is legal FITS but unsupported here; patch the card in a valid file
        byte[] fits = FitsTestData.BuildFits(4, 4, (x, y) => 100, bitpix: 32);
        PatchCard(fits, "BITPIX", "64");

        var ex = Assert.Throws<Exception>(() => Load(fits));
        Assert.Contains("BITPIX neacceptat", ex.Message);
    }

    // ---------------------------------------------------------------- BZERO / BSCALE

    [Fact]
    public void Load_AppliesTheUnsigned16BitConvention_BzeroOf32768()
    {
        // what Siril writes: stored = physical - 32768
        var img = Load(FitsTestData.BuildFits(4, 4, (x, y) => 40000, bitpix: 16, bzero: 32768));

        Assert.Equal(40000, img.At(0, 0), 6);
    }

    [Fact]
    public void Load_AppliesBscale()
    {
        var img = Load(FitsTestData.BuildFits(4, 4, (x, y) => 5000, bitpix: 16, bzero: 0, bscale: 4));

        Assert.Equal(5000, img.At(0, 0), 6);
    }

    [Fact]
    public void Load_DefaultsBscaleToOneAndBzeroToZero_WhenTheCardsAreAbsent()
    {
        // hand-build a header with neither BZERO nor BSCALE
        var cards = new[]
        {
            FitsTestData.Card("SIMPLE", "T"), FitsTestData.Card("BITPIX", "16"),
            FitsTestData.Card("NAXIS", "2"), FitsTestData.Card("NAXIS1", "4"),
            FitsTestData.Card("NAXIS2", "4"),
        };
        byte[] fits = BuildRaw(cards, Sixteen(4 * 4, 1234));

        var img = Load(fits);

        Assert.Equal(1234, img.At(0, 0), 6);
    }

    // ---------------------------------------------------------------- colour

    [Fact]
    public void Load_AveragesColourPlanes()
    {
        // plane values 100 / 200 / 300 -> mean 200
        var img = Load(FitsTestData.BuildFitsCube(4, 4, 3, (x, y, p) => 100 * (p + 1)));

        Assert.Equal(4, img.W);
        Assert.Equal(4, img.H);
        Assert.Equal(200, img.At(2, 2), 6);
    }

    [Fact]
    public void Load_TreatsANaxis3OfOneTheSameAsAMonoImage()
    {
        var cube = Load(FitsTestData.BuildFitsCube(4, 4, 1, (x, y, p) => 777));
        var mono = Load(FitsTestData.BuildFits(4, 4, (x, y) => 777));

        Assert.Equal(mono.At(1, 1), cube.At(1, 1), 6);
        Assert.Equal(mono.W, cube.W);
        Assert.Equal(mono.H, cube.H);
    }

    // ---------------------------------------------------------------- HDU walking

    [Fact]
    public void Load_SkipsAnEmptyPrimaryAndReadsTheImageExtension()
    {
        var img = Load(FitsTestData.BuildFitsWithEmptyPrimary(8, 6, (x, y) => 4242));

        Assert.Equal(8, img.W);
        Assert.Equal(6, img.H);
        Assert.Equal(4242, img.At(3, 3), 6);
    }

    [Fact]
    public void Load_HandlesAHeaderSpanningMoreThanOne2880Block()
    {
        // 40 filler cards push the header past the 36-card block boundary
        var filler = Enumerable.Range(0, 40)
            .Select(i => FitsTestData.Card("FILLER" + (i % 10), i.ToString()));

        var img = Load(FitsTestData.BuildFits(4, 4, (x, y) => 999, extraCards: filler));

        Assert.Equal(999, img.At(0, 0), 6);
    }

    [Fact]
    public void Load_ThrowsOnATruncatedFile()
    {
        byte[] fits = FitsTestData.BuildFits(64, 64, (x, y) => 100);
        byte[] truncated = fits.Take(FitsTestData.BlockSize + 100).ToArray();

        // the R-009 refusal, not the IndexOutOfRangeException the sample loop used to throw
        Assert.Throws<InvalidDataException>(() => Load(truncated));
    }

    /// <summary>One HDU: the cards, END, the padding, then <paramref name="dataBytes"/> bytes
    /// that are not zero (so a table read as an image would show), padded to a block.</summary>
    static byte[] Hdu(IEnumerable<string> cards, int dataBytes)
    {
        var head = new System.Text.StringBuilder();
        foreach (string c in cards) head.Append(c.PadRight(FitsTestData.CardSize));
        head.Append("END".PadRight(FitsTestData.CardSize));
        while (head.Length % FitsTestData.BlockSize != 0) head.Append(' ');
        byte[] h = System.Text.Encoding.ASCII.GetBytes(head.ToString());
        var all = new byte[h.Length + (dataBytes + FitsTestData.BlockSize - 1) / FitsTestData.BlockSize * FitsTestData.BlockSize];
        Array.Copy(h, all, h.Length);
        for (int i = 0; i < dataBytes; i++) all[h.Length + i] = (byte)(i * 37 % 251 + 1);
        return all;
    }

    /// <summary>An empty primary HDU, as a file whose image is in an extension starts.</summary>
    static byte[] EmptyPrimary() => Hdu(new[]
    {
        FitsTestData.Card("SIMPLE", "T"), FitsTestData.Card("BITPIX", "16"), FitsTestData.Card("NAXIS", "0"), FitsTestData.Card("EXTEND", "T"),
    }, 0);

    /// <summary>A binary table of <paramref name="rows"/> rows of 8 bytes with a heap, the
    /// shape of a tile-compressed image (one variable-length column); with
    /// <paramref name="compressedImage"/> it carries the ZIMAGE cards fpack and Siril write.</summary>
    static byte[] Table(int rows, int heap, bool compressedImage, int imageWidth = 1536)
    {
        var cards = new List<string>
        {
            "XTENSION= 'BINTABLE'", FitsTestData.Card("BITPIX", "8"), FitsTestData.Card("NAXIS", "2"),
            FitsTestData.Card("NAXIS1", "8"), FitsTestData.Card("NAXIS2", rows.ToString()),
            FitsTestData.Card("PCOUNT", heap.ToString()), FitsTestData.Card("GCOUNT", "1"), FitsTestData.Card("TFIELDS", "1"),
            "TTYPE1  = 'COMPRESSED_DATA'", "TFORM1  = '1PB(2500)'",
        };
        if (compressedImage)
            cards.AddRange(new[]
            {
                FitsTestData.Card("ZIMAGE", "T"), FitsTestData.Card("ZBITPIX", "16"), FitsTestData.Card("ZNAXIS", "2"),
                FitsTestData.Card("ZNAXIS1", imageWidth.ToString()), FitsTestData.Card("ZNAXIS2", rows.ToString()), "ZCMPTYPE= 'RICE_1  '",
            });
        return Hdu(cards, 8 * rows + heap);
    }

    /// <summary>An image HDU made into an extension: the first card, SIMPLE, replaced by
    /// XTENSION = 'IMAGE'.</summary>
    static byte[] ImageExtension(int w, int h, Func<int, int, double> pixel)
    {
        byte[] hdu = FitsTestData.BuildFits(w, h, pixel, bitpix: 16);
        byte[] card = System.Text.Encoding.ASCII.GetBytes("XTENSION= 'IMAGE   '".PadRight(FitsTestData.CardSize));
        Array.Copy(card, hdu, card.Length);
        return hdu;
    }

    /// <summary>
    /// R-136: a tile-compressed FITS, as fpack and Siril's FITS compression write it, is refused
    /// by name. Before, the table's rows were read as an image 8 pixels wide (its BITPIX is 8,
    /// its NAXIS1 is 8) and every mode went on with the bytes of the heap descriptors.
    /// </summary>
    [Fact]
    public void R136_ATileCompressedFitsIsRefusedByName()
    {
        byte[] file = EmptyPrimary().Concat(Table(rows: 1024, heap: 400000, compressedImage: true)).ToArray();

        var ex = Assert.Throws<InvalidDataException>(() => Load(file));
        Assert.Equal("FITS: imaginea e stocata comprimat (compresie pe dale, cum o scriu compresia FITS din Siril sau fpack), "
                   + "iar FlatCheck nu o citeste. Salveaza fisierul fara compresie.", ex.Message);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("FITS: the image is stored compressed (tile compression, as Siril's FITS compression or fpack write it), "
                       + "which FlatCheck does not read. Save the file without compression.",
                         Assert.Throws<InvalidDataException>(() => Load(file)).Message);
    }

    /// <summary>R-136 from the console: every mode stops at the same sentence, exit 1, and
    /// prints no verdict and writes no picture. The structure mode printed REDO, exit 3, and
    /// --histeq wrote a picture, exit 0.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("--expo")]
    [InlineData("--histeq")]
    [InlineData("--light")]
    public void R136_EveryModeOfTheConsoleRefusesACompressedFits(string mode)
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("compressed.fit", EmptyPrimary().Concat(Table(rows: 1024, heap: 400000, compressedImage: true)).ToArray());
        string png = ws.PathTo("out.png");
        var args = new List<string> { path, "-o", png };
        if (mode.Length > 0) args.Add(mode);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        int exit = FlatCheckCli.Run(args.ToArray(), stdout, stderr);

        Assert.Equal(1, exit);
        Assert.Contains("FITS: imaginea e stocata comprimat", stderr.ToString());
        Assert.DoesNotContain("VERDICT", stdout.ToString());
        Assert.False(File.Exists(png));
    }

    /// <summary>
    /// R-136: a table that is not a compressed image is skipped whole, heap included, and the
    /// image extension after it is the image. Before, the table was the image; and had it been
    /// skipped by its NAXISn alone, the heap would have been parsed as the next header.
    /// </summary>
    [Fact]
    public void R136_APlainTableIsSkippedWithItsHeap_AndTheImageExtensionAfterItIsRead()
    {
        byte[] file = EmptyPrimary()
            .Concat(Table(rows: 100, heap: 5000, compressedImage: false))
            .Concat(ImageExtension(6, 4, (x, y) => 100 + 10 * y + x)).ToArray();

        Img img = Load(file);

        Assert.Equal(6, img.W);
        Assert.Equal(4, img.H);
        Assert.Equal(100, img.At(0, 0), 6);      // the fixture writes the scene as the raster reads it (bottom-up)
        Assert.Equal(135, img.At(3, 5), 6);
        Assert.Equal(113, img.At(1, 3), 6);
    }

    /// <summary>R-136: a table alone is no image.</summary>
    [Fact]
    public void R136_AFileWithOnlyATableHasNoImage()
    {
        byte[] file = EmptyPrimary().Concat(Table(rows: 100, heap: 5000, compressedImage: false)).ToArray();

        var ex = Assert.Throws<Exception>(() => Load(file));

        Assert.Equal("FITS: niciun HDU cu imagine 2D gasit.", ex.Message);
    }

    /// <summary>A table extension with the given counts and no data after its header.</summary>
    static byte[] TableHeader(string naxis, string naxis1, string naxis2, string pcount, string gcount, string naxis3 = "")
    {
        var cards = new List<string>
        {
            "XTENSION= 'BINTABLE'", FitsTestData.Card("BITPIX", "8"), FitsTestData.Card("NAXIS", naxis),
            FitsTestData.Card("NAXIS1", naxis1), FitsTestData.Card("NAXIS2", naxis2),
        };
        if (naxis3.Length > 0) cards.Add(FitsTestData.Card("NAXIS3", naxis3));
        cards.Add(FitsTestData.Card("PCOUNT", pcount));
        cards.Add(FitsTestData.Card("GCOUNT", gcount));
        return Hdu(cards, 0);
    }

    /// <summary>What the reader throws on a file, or a failure when it has not come back in 20
    /// seconds: a walk that steps back onto the header it has just read never ends.</summary>
    static async Task<Exception> ThrownBy(byte[] file)
    {
        Task<Exception> read = Task.Run(() => Assert.ThrowsAny<Exception>(() => Load(file)));
        Task first = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.True(ReferenceEquals(first, read), "the reader did not come back");
        return await read;
    }

    /// <summary>
    /// R-136: a table whose counts are below zero cannot send the walk back over its own
    /// header. Each row would have made the step one block backwards (-5760 bytes of table,
    /// padded, is -2880): through PCOUNT, through a NAXISn, through GCOUNT. The counts are taken
    /// as zero and the file ends as one without an image.
    /// </summary>
    [Theory]
    [InlineData("8", "0", "-5760", "1")]
    [InlineData("-2880", "2", "0", "1")]
    [InlineData("8", "720", "0", "-1")]
    public async Task R136_ATableWithNegativeCountsDoesNotSendTheWalkBackwards(string naxis1, string naxis2, string pcount, string gcount)
    {
        byte[] file = EmptyPrimary().Concat(TableHeader("2", naxis1, naxis2, pcount, gcount)).ToArray();

        Exception e = await ThrownBy(file);

        Assert.Equal("FITS: niciun HDU cu imagine 2D gasit.", e.Message);
    }

    /// <summary>
    /// R-136: counts that are all positive and whose product does not fit 64 bits (it wraps to
    /// a step of exactly -2880 with these four, found by the second review of the branch) are
    /// more bytes than any file holds: the cut-data refusal, not a walk that never ends.
    /// </summary>
    [Fact]
    public async Task R136_ATableWhoseSizeOverflowsIsRefusedAsCutData()
    {
        byte[] file = EmptyPrimary().Concat(TableHeader("3", "1784854093", "2057604577", "1903472180", "1", naxis3: "1133817633")).ToArray();

        Exception e = await ThrownBy(file);

        Assert.IsType<InvalidDataException>(e);
        Assert.StartsWith("FITS: date trunchiate", e.Message);
    }

    // ---------------------------------------------------------------- a header that cannot be right (R-139)

    /// <summary>A primary HDU from the cards given after SIMPLE, with no data after it.</summary>
    static byte[] Primary(params (string Key, string Value)[] cards)
        => Hdu(new[] { FitsTestData.Card("SIMPLE", "T") }.Concat(cards.Select(c => FitsTestData.Card(c.Key, c.Value))), 0);

    /// <summary>
    /// R-139, the header of the review's CR-013: an axis of -2880 bytes makes the data area
    /// -5760 bytes, which padded is one block backwards, onto the header just read. The
    /// reader never came back; it now refuses the axis by name, in both languages.
    /// </summary>
    [Fact]
    public async Task R139_ANegativeAxisIsRefusedByNameInsteadOfSendingTheWalkBackwards()
    {
        byte[] file = Primary(("BITPIX", "8"), ("NAXIS", "2"), ("NAXIS1", "-2880"), ("NAXIS2", "2"));

        Exception e = await ThrownBy(file);

        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("FITS: header invalid: NAXIS1 = -2880; o axa nu poate fi mai scurta de 0.", e.Message);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("FITS: invalid header: NAXIS1 = -2880; an axis cannot be shorter than 0.", (await ThrownBy(file)).Message);
    }

    /// <summary>R-139: any axis, of an image or of an HDU that would only be stepped over.</summary>
    [Theory]
    [InlineData("2", "4", "-1", "", "NAXIS2 = -1")]
    [InlineData("3", "4", "4", "-3", "NAXIS3 = -3")]
    [InlineData("1", "-100", "", "", "NAXIS1 = -100")]
    public async Task R139_ANegativeAxisIsRefusedWhicheverItIs(string naxis, string n1, string n2, string n3, string named)
    {
        var cards = new List<(string, string)> { ("BITPIX", "16"), ("NAXIS", naxis), ("NAXIS1", n1) };
        if (n2.Length > 0) cards.Add(("NAXIS2", n2));
        if (n3.Length > 0) cards.Add(("NAXIS3", n3));

        Exception e = await ThrownBy(Primary(cards.ToArray()));

        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("FITS: header invalid: " + named + "; o axa nu poate fi mai scurta de 0.", e.Message);
    }

    /// <summary>R-139: the standard allows 0 to 999 axes. A count of two thousand million
    /// was a loop of as many header lookups, and a count below zero was read as a header
    /// with one byte of data.</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("1000")]
    [InlineData("2000000000")]
    public async Task R139_AnAxisCountOutsideTheStandardIsRefused(string naxis)
    {
        Exception e = await ThrownBy(Primary(("BITPIX", "16"), ("NAXIS", naxis), ("NAXIS1", "4"), ("NAXIS2", "4")));

        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("FITS: header invalid: NAXIS = " + naxis + "; standardul permite intre 0 si 999 de axe.", e.Message);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("FITS: invalid header: NAXIS = " + naxis + "; the standard allows 0 to 999 axes.",
                         (await ThrownBy(Primary(("BITPIX", "16"), ("NAXIS", naxis), ("NAXIS1", "4"), ("NAXIS2", "4")))).Message);
    }

    /// <summary>
    /// R-139: without BITPIX a sample has no size, so the bound of R-009 saw 0 bytes of data
    /// and let any NAXIS1 x NAXIS2 through to the allocation: an out-of-memory error for
    /// 100000 x 100000. Refused by name now, missing or not a number, before any size is
    /// computed.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("'sixteen'")]
    public async Task R139_AHeaderWithoutAUsableBitpixIsRefusedBeforeAnyPixelIsAllocated(string? bitpix)
    {
        var cards = new List<(string, string)> { ("NAXIS", "2"), ("NAXIS1", "100000"), ("NAXIS2", "100000") };
        if (bitpix != null) cards.Insert(0, ("BITPIX", bitpix));

        Exception e = await ThrownBy(Primary(cards.ToArray()));

        Assert.IsType<InvalidDataException>(e);
        Assert.Equal("FITS: header invalid: BITPIX lipseste sau nu e un numar.", e.Message);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("FITS: invalid header: BITPIX is missing or not a number.", (await ThrownBy(Primary(cards.ToArray()))).Message);
    }

    /// <summary>R-139: a BITPIX that is none of the standard's six gives no sample size
    /// either, so an HDU that would only be stepped over is refused too; it was stepped over
    /// by a size that meant nothing.</summary>
    [Fact]
    public async Task R139_ABitpixOutsideTheStandardIsRefusedInAnHduThatIsOnlySteppedOver()
    {
        Exception e = await ThrownBy(Primary(("BITPIX", "12"), ("NAXIS", "1"), ("NAXIS1", "100")));

        Assert.Equal("BITPIX neacceptat: 12", e.Message);
    }

    /// <summary>R-139: a size that does not fit 64 bits is more bytes than any file holds,
    /// the cut-data refusal of R-009, whatever the product wraps to.</summary>
    [Fact]
    public async Task R139_AnImageWhoseSizeOverflowsIsRefusedAsCutData()
    {
        Exception e = await ThrownBy(Primary(("BITPIX", "-64"), ("NAXIS", "3"), ("NAXIS1", "2147483647"), ("NAXIS2", "2147483647"), ("NAXIS3", "2147483647")));

        Assert.IsType<InvalidDataException>(e);
        Assert.StartsWith("FITS: date trunchiate", e.Message);
    }

    /// <summary>R-139: an axis of length 0 beside a 2D size, or a second axis card in a
    /// header that declares one axis, is no image: the HDU is stepped over and the file ends
    /// as one without an image. Both ended in an ArgumentException out of the row copy.</summary>
    [Theory]
    [InlineData("3", "0", 0)]      // 4 x 4 x 0: no data after the header
    [InlineData("1", "", 1)]       // one axis of 4 samples: 8 bytes, one block
    public async Task R139_AnHduWithAnEmptyAxisOrASingleAxisHoldsNoImage(string naxis, string n3, int dataBlocks)
    {
        var cards = new List<(string, string)> { ("BITPIX", "16"), ("NAXIS", naxis), ("NAXIS1", "4"), ("NAXIS2", "4") };
        if (n3.Length > 0) cards.Add(("NAXIS3", n3));
        byte[] file = Primary(cards.ToArray()).Concat(new byte[dataBlocks * FitsTestData.BlockSize]).ToArray();

        Exception e = await ThrownBy(file);

        Assert.Equal("FITS: niciun HDU cu imagine 2D gasit.", e.Message);
    }

    /// <summary>R-139 from the console: exit 1 and the sentence, from every mode, where the
    /// run never ended.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("--expo")]
    [InlineData("--histeq")]
    [InlineData("--light")]
    public async Task R139_EveryModeOfTheConsoleRefusesABrokenHeader(string mode)
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("broken.fit", Primary(("BITPIX", "8"), ("NAXIS", "2"), ("NAXIS1", "-2880"), ("NAXIS2", "2")));
        var args = new List<string> { path, "-o", ws.PathTo("out.png") };
        if (mode.Length > 0) args.Add(mode);
        var stderr = new StringWriter();

        Task<int> run = Task.Run(() => FlatCheckCli.Run(args.ToArray(), new StringWriter(), stderr));
        Assert.True(ReferenceEquals(await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(20))), run), "the run did not come back");

        Assert.Equal(1, await run);
        Assert.Contains("FITS: header invalid: NAXIS1 = -2880", stderr.ToString());
        Assert.False(File.Exists(ws.PathTo("out.png")));
    }

    /// <summary>R-136: a table cut short is the cut file of R-009, heap counted.</summary>
    [Fact]
    public void R136_ATableCutShortIsRefusedAsCutData()
    {
        byte[] whole = EmptyPrimary().Concat(Table(rows: 100, heap: 5000, compressedImage: false)).ToArray();
        byte[] cut = whole.Take(whole.Length - FitsTestData.BlockSize).ToArray();   // 5800 bytes of table, 2880 fewer in the file

        var ex = Assert.Throws<InvalidDataException>(() => Load(cut));

        Assert.StartsWith("FITS: date trunchiate", ex.Message);
        Assert.Contains("5800", ex.Message);
    }

    /// <summary>
    /// R-009: a valid header for a 4 x 4 16-bit image (32 bytes of pixels) followed by only 8
    /// data bytes is refused with InvalidDataException naming both counts, before any sample
    /// is read; it used to be an IndexOutOfRangeException out of the sample loop.
    /// </summary>
    [Fact]
    public void R009_LoadRefusesAFitsWhoseDataIsCut()
    {
        byte[] fits = FitsTestData.BuildFits(4, 4, (x, y) => 100, bitpix: 16);
        byte[] cut = fits.Take(FitsTestData.BlockSize + 8).ToArray();

        var ex = Assert.Throws<InvalidDataException>(() => Load(cut));

        Assert.StartsWith("FITS:", ex.Message);
        Assert.Contains("32", ex.Message);
        Assert.EndsWith(" 8.", ex.Message);
    }

    /// <summary>
    /// R-009: the check counts the data alone, so a last HDU with every pixel but without its
    /// padding to 2880 bytes still loads.
    /// </summary>
    [Fact]
    public void R009_LoadReadsAFitsWhoseLastPaddingIsMissing()
    {
        byte[] fits = FitsTestData.BuildFits(4, 4, (x, y) => 100, bitpix: 16);
        byte[] unpadded = fits.Take(FitsTestData.BlockSize + 32).ToArray();

        var img = Load(unpadded);

        Assert.Equal(4, img.W);
        Assert.Equal(100, img.At(3, 3), 6);
    }

    /// <summary>
    /// R-009: the check runs for every HDU, the skipped ones too. A 1-D HDU of 100 16-bit
    /// samples (200 bytes) cut after 100 bytes is refused with the two counts, not skipped
    /// into "no HDU with a 2D image".
    /// </summary>
    [Fact]
    public void R009_LoadRefusesACutHduWithoutA2DImage()
    {
        byte[] fits = FitsTestData.BuildFitsNaxis1(100);
        byte[] cut = fits.Take(FitsTestData.BlockSize + 100).ToArray();

        var ex = Assert.Throws<InvalidDataException>(() => Load(cut));

        Assert.Contains("200", ex.Message);
        Assert.EndsWith(" 100.", ex.Message);
    }

    /// <summary>
    /// Phase 8. npix is now the product of all NAXISn, so a 1-D HDU advances past its
    /// data instead of leaving pos parked and re-parsing the data as a header. Before,
    /// this failed with the misleading "header incomplet".
    /// </summary>
    [Fact]
    public void Naxis1_ReportsThatThereIsNo2DImage()
    {
        var ex = Assert.Throws<Exception>(() => Load(FitsTestData.BuildFitsNaxis1(100)));

        Assert.Contains("niciun HDU cu imagine 2D", ex.Message);
    }

    /// <summary>
    /// Phase 8: the card loop stops at END instead of scanning the rest of the block.
    /// </summary>
    [Fact]
    public void CardsAfterEndAreIgnored()
    {
        // a bogus NAXIS1 planted after END would corrupt the geometry if it were read
        byte[] good = FitsTestData.BuildFits(4, 4, (x, y) => 1234);
        byte[] tampered = (byte[])good.Clone();
        string junk = FitsTestData.Card("NAXIS1", "999");
        int endCard = FindCard(tampered, "END");
        System.Text.Encoding.ASCII.GetBytes(junk, 0, FitsTestData.CardSize,
            tampered, (endCard + 1) * FitsTestData.CardSize);

        var img = Load(tampered);

        Assert.Equal(4, img.W);
        Assert.Equal(1234, img.At(0, 0), 6);
    }

    static int FindCard(byte[] fits, string key)
    {
        for (int c = 0; c < 36; c++)
            if (System.Text.Encoding.ASCII.GetString(fits, c * FitsTestData.CardSize, 8).Trim() == key)
                return c;
        throw new InvalidOperationException("card not found: " + key);
    }

    // ---------------------------------------------------------------- path overload

    [Fact]
    public void Load_FromPath_MatchesLoadFromBytes()
    {
        using var ws = new TempWorkspace();
        byte[] bytes = FitsTestData.BuildFits(16, 16, FitsTestData.Radial(16, 16));
        string path = ws.WriteFile("f.fit", bytes);

        var fromPath = new FitsReader().Load(path).Image;
        var fromBytes = FitsReader.Load(bytes);

        Assert.Equal(fromBytes.W, fromPath.W);
        Assert.Equal(fromBytes.H, fromPath.H);
        Assert.Equal(fromBytes.D, fromPath.D);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Overwrites a card's value in place, keeping the 80-byte layout intact.</summary>
    static void PatchCard(byte[] fits, string key, string value)
    {
        string card = FitsTestData.Card(key, value);
        for (int c = 0; c < 36; c++)
        {
            int off = c * FitsTestData.CardSize;
            string existing = System.Text.Encoding.ASCII.GetString(fits, off, 8).Trim();
            if (existing == key)
            {
                System.Text.Encoding.ASCII.GetBytes(card, 0, FitsTestData.CardSize, fits, off);
                return;
            }
        }
        throw new InvalidOperationException("card not found: " + key);
    }

    // ---------------------------------------------------------------- the header

    /// <summary>
    /// The header was until now parsed and thrown away. --light needs it to find the focal
    /// length and the scale, so that it can refuse a frame the test does not apply to.
    /// </summary>
    [Fact]
    public void LoadWithHeader_ReturnsTheCardsOfTheHduThatHeldTheImage()
    {
        byte[] fits = FitsTestData.BuildFits(
            32, 32, FitsTestData.Constant(1000),
            extraCards: FitsTestData.OpticsCards(135, pixelUm: 3.92));

        var (img, hdr) = FitsReader.LoadWithHeader(fits);

        Assert.Equal(32, img.W);
        Assert.Equal(135, FitsReader.HDbl(hdr, "FOCALLEN", 0));
        Assert.Equal(3.92, FitsReader.HDbl(hdr, "XPIXSZ", 0), 3);
        Assert.Equal(32, FitsReader.HInt(hdr, "NAXIS1", 0));
    }

    /// <summary>A minimal header is not an error - it just lacks the optional keys.</summary>
    [Fact]
    public void LoadWithHeader_OnAMinimalHeader_SimplyLacksTheOpticalKeys()
    {
        var (_, hdr) = FitsReader.LoadWithHeader(
            FitsTestData.BuildFits(16, 16, FitsTestData.Constant(500)));

        Assert.False(hdr.ContainsKey("FOCALLEN"));
        Assert.Equal(0, FitsReader.HDbl(hdr, "FOCALLEN", 0));
    }

    /// <summary>Load stays exactly what it was: the same raster, the header just
    /// ignored.</summary>
    [Fact]
    public void Load_StillReturnsTheSameRasterAsLoadWithHeader()
    {
        byte[] fits = FitsTestData.BuildFits(16, 16, FitsTestData.Radial(16, 16));

        Assert.Equal(FitsReader.LoadWithHeader(fits).Image.D, FitsReader.Load(fits).D);
    }

    // ---------------------------------------------------------------- BAYERPAT

    /// <summary>
    /// Siril writes BAYERPAT on a frame that came through the Conversion tab, so the
    /// exposure mode can name the four positions instead of numbering them. The fixture
    /// carries the header Siril really writes, ROWORDER 'BOTTOM-UP' included: without it an
    /// even height reads the pattern shifted by one row (R-044), which is the next tests' job.
    /// </summary>
    [Fact]
    public void ACfaFitsCarriesTheMosaicOrder()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, 16, FitsTestData.Constant(500),
            extraCards: FitsTestData.BayerCards("RGGB", rowOrder: "BOTTOM-UP")));

        LoadedFrame f = new FitsReader().Load(path);

        Assert.NotNull(f.Cfa);
        Assert.Equal("RGGB", f.Cfa.Letters);
        Assert.Equal("BAYERPAT", f.Cfa.Source);
    }

    /// <summary>
    /// The offsets are not decoration: a crop that starts on an odd column shifts the
    /// mosaic phase, so the same BAYERPAT then describes a different raster.
    /// </summary>
    [Fact]
    public void TheBayerOffsetsShiftTheOrder()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, 16, FitsTestData.Constant(500),
            extraCards: FitsTestData.BayerCards("RGGB", xOffset: 1, rowOrder: "BOTTOM-UP")));

        Assert.Equal("GRBG", new FitsReader().Load(path).Cfa.Letters);
    }

    /// <summary>
    /// The header Siril actually writes on a converted or stacked frame: BAYERPAT RGGB and
    /// ROWORDER 'BOTTOM-UP'. Read with the file's first row last (R-130), the raster is the
    /// sensor seen top-down, which is exactly what BAYERPAT describes, so the pattern applies as
    /// written at any height (R-044). Measured on pp_flat_stacked.fit (6016x4016): read in file
    /// order and unshifted, the channel labelled G2 sat at a third of the green, exactly where
    /// --expo on the source NEF puts R; the flip puts R back at [0,0], where the NEF has it.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(15)]
    public void R044_ABottomUpFrameKeepsBayerpatAsWritten_AtAnyHeight(int height)
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, height, FitsTestData.Constant(500),
            extraCards: FitsTestData.BayerCards("RGGB", rowOrder: "BOTTOM-UP")));

        CfaPattern cfa = new FitsReader().Load(path).Cfa;

        Assert.Equal("RGGB", cfa.Letters);
        Assert.Equal("R", cfa.Name(0));      // [0,0]
        Assert.Equal("BAYERPAT", cfa.Source);
    }

    /// <summary>
    /// A file without the card, or saying TOP-DOWN, is read upside down against its own
    /// BAYERPAT (as Siril shows it, R-130): on an even height raster row 0 is an odd sensor row
    /// and the phase moves by one row; on an odd height the last row has the first row's parity
    /// and nothing moves.
    /// </summary>
    [Theory]
    [InlineData(16, null, "GBRG")]
    [InlineData(16, "TOP-DOWN", "GBRG")]
    [InlineData(15, null, "RGGB")]
    public void R044_AFrameNotWrittenBottomUp_ShiftsTheOrderByOneRowOnAnEvenHeight(int height, string rowOrder, string expected)
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, height, FitsTestData.Constant(500),
            extraCards: FitsTestData.BayerCards("RGGB", rowOrder: rowOrder)));

        Assert.Equal(expected, new FitsReader().Load(path).Cfa.Letters);
    }

    /// <summary>
    /// A debayered frame carries no BAYERPAT - correctly, there is no mosaic left to
    /// name - and so does every synthetic fixture in this suite. That must stay a plain
    /// "unknown" and change nothing else about the load.
    /// </summary>
    [Fact]
    public void WithoutTheCardTheOrderIsSimplyUnknown()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("plain.fit",
            FitsTestData.BuildFits(16, 16, FitsTestData.Constant(500)));

        LoadedFrame f = new FitsReader().Load(path);

        Assert.Null(f.Cfa);
        Assert.Null(f.Scale);
        Assert.NotNull(f.Image);
    }

    static byte[] Sixteen(int count, short value)
    {
        var b = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            b[i * 2] = (byte)((value >> 8) & 0xFF);
            b[i * 2 + 1] = (byte)(value & 0xFF);
        }
        return b;
    }

    static byte[] BuildRaw(IEnumerable<string> cards, byte[] data)
    {
        var list = cards.ToList();
        list.Add("END".PadRight(FitsTestData.CardSize));
        while (list.Count % 36 != 0) list.Add(new string(' ', FitsTestData.CardSize));
        byte[] hdr = System.Text.Encoding.ASCII.GetBytes(string.Concat(list));

        int pad = (FitsTestData.BlockSize - (data.Length % FitsTestData.BlockSize)) % FitsTestData.BlockSize;
        return hdr.Concat(data).Concat(new byte[pad]).ToArray();
    }
}
