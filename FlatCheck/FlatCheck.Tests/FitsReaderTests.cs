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

    /// <summary>Phase 8: the card loop stops at END instead of scanning the rest of the block.</summary>
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

        var fromPath = FitsReader.Load(path);
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
