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
        // value encodes its own coordinates so a transpose would be obvious
        var img = Load(FitsTestData.BuildFits(4, 3, (x, y) => 1000 + y * 10 + x));

        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                Assert.Equal(1000 + y * 10 + x, img.At(y, x), 6);
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

        Assert.ThrowsAny<Exception>(() => Load(truncated));
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
    /// exposure mode can name the four positions instead of numbering them.
    /// </summary>
    [Fact]
    public void ACfaFitsCarriesTheMosaicOrder()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, 16, FitsTestData.Constant(500), extraCards: FitsTestData.BayerCards("RGGB")));

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
            extraCards: FitsTestData.BayerCards("RGGB", xOffset: 1)));

        Assert.Equal("GRBG", new FitsReader().Load(path).Cfa.Letters);
    }

    /// <summary>
    /// The header Siril actually writes on a converted or stacked frame: BAYERPAT RGGB
    /// and ROWORDER 'BOTTOM-UP'. Read in file order on an even height, the row parity
    /// flips, so position [1,0] - which BAYERPAT calls G - is the real R. Measured on
    /// pp_flat_stacked.fit (6016x4016): the channel labelled G2 sat at a third of the
    /// green, exactly where --expo on the source NEF puts R.
    /// </summary>
    [Fact]
    public void ABottomUpFrameOfEvenHeightShiftsTheOrderByOneRow()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, 16, FitsTestData.Constant(500),
            extraCards: FitsTestData.BayerCards("RGGB", rowOrder: "BOTTOM-UP")));

        CfaPattern cfa = new FitsReader().Load(path).Cfa;

        Assert.Equal("GBRG", cfa.Letters);
        Assert.Equal("R", cfa.Name(2));      // [1,0]
        Assert.Equal("BAYERPAT", cfa.Source);
    }

    /// <summary>On an odd height the bottom row has the top row's parity: no shift.</summary>
    [Fact]
    public void ABottomUpFrameOfOddHeightKeepsTheOrder()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("cfa.fit", FitsTestData.BuildFits(
            16, 15, FitsTestData.Constant(500),
            extraCards: FitsTestData.BayerCards("RGGB", rowOrder: "BOTTOM-UP")));

        Assert.Equal("RGGB", new FitsReader().Load(path).Cfa.Letters);
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
