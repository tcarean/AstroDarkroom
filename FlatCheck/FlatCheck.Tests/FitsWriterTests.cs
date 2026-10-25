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
/// The FITS writer, checked through the FITS reader: what goes out must come back in
/// unchanged, and the input's header must travel with it. Also the reader's new raw-card
/// output, since that is what the writer copies.
/// </summary>
public class FitsWriterTests
{
    static Img Ramp(int w, int h) => TestImages.From(w, h, (x, y) => 1000 + 3.25 * x + 0.5 * y);

    // ---------------------------------------------------------------- the reader's cards

    /// <summary>
    /// The raw cards are the whole 80 characters, in file order, END excluded - including
    /// a COMMENT with a slash in it, which the parsed dictionary would have truncated.
    /// </summary>
    [Fact]
    public void LoadFull_ReturnsTheRawCardsVerbatim()
    {
        string comment = "COMMENT  path = C:/astro/flats, keep the slash".PadRight(80);
        byte[] fits = FitsTestData.BuildFits(4, 3, FitsTestData.Constant(5),
            extraCards: new[] { comment, FitsTestData.Card("BAYERPAT", "'RGGB'") });

        var (_, hdr, cards, planes, bitpix) = FitsReader.LoadFull(fits);

        Assert.Contains(comment, cards);
        Assert.Contains(cards, c => c.StartsWith("BAYERPAT"));
        Assert.DoesNotContain(cards, c => c.StartsWith("END"));
        Assert.All(cards, c => Assert.Equal(80, c.Length));
        Assert.Equal(1, planes);
        Assert.Equal(16, bitpix);
        Assert.Equal("RGGB", hdr["BAYERPAT"]);
    }

    [Fact]
    public void LoadFull_ReportsThePlaneCountOfACube()
    {
        var (_, _, _, planes, _) = FitsReader.LoadFull(FitsTestData.BuildFitsCube(4, 4, 3, (x, y, p) => 100));

        Assert.Equal(3, planes);
    }

    [Fact]
    public void LoadedFrame_CarriesCardsPlanesAndBitpix()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("f.fit", FitsTestData.BuildFits(4, 4, FitsTestData.Constant(0.5), bitpix: -32, bzero: 0));

        LoadedFrame f = ImageLoader.Load(path);

        Assert.NotEmpty(f.Cards);
        Assert.Equal(1, f.Planes);
        Assert.Equal(-32, f.Bitpix);
    }

    // ---------------------------------------------------------------- round trips

    [Fact]
    public void Float_RoundTripsBitForBit()
    {
        Img img = Ramp(37, 23);

        byte[] fits = FitsWriter.Encode(img, -32, null, null);
        var (back, hdr, _, planes, bitpix) = FitsReader.LoadFull(fits);

        Assert.Equal(-32, bitpix);
        Assert.Equal(1, planes);
        Assert.Equal(23, back.H);
        Assert.Equal(37, back.W);
        for (int i = 0; i < img.D.Length; i++)
            Assert.Equal((double)(float)img.D[i], back.D[i]);
        Assert.Equal("37", hdr["NAXIS1"]);
        Assert.Equal("23", hdr["NAXIS2"]);
    }

    [Fact]
    public void SixteenBit_RoundTripsToHalfAnAdu_WithTheUnsignedOffset()
    {
        Img img = Ramp(9, 5);

        byte[] fits = FitsWriter.Encode(img, 16, null, null);
        var (back, hdr, _, _, bitpix) = FitsReader.LoadFull(fits);

        Assert.Equal(16, bitpix);
        Assert.Equal("32768", hdr["BZERO"]);
        for (int i = 0; i < img.D.Length; i++)
            Assert.InRange(back.D[i], img.D[i] - 0.5, img.D[i] + 0.5);
    }

    [Fact]
    public void SixteenBit_ClampsToTheUnsignedRange()
    {
        var img = TestImages.From(2, 1, (x, y) => x == 0 ? -50 : 70000);

        var (back, _, _, _, _) = FitsReader.LoadFull(FitsWriter.Encode(img, 16, null, null));

        Assert.Equal(0, back.D[0]);
        Assert.Equal(65535, back.D[1]);
    }

    [Fact]
    public void OnlyTheTwoSupportedTypesAreWritten()
    {
        Assert.Throws<ArgumentException>(() => FitsWriter.Encode(Ramp(2, 2), 32, null, null));
        Assert.Throws<ArgumentException>(() => FitsWriter.Encode(Ramp(2, 2), -64, null, null));
    }

    // ---------------------------------------------------------------- the header

    /// <summary>
    /// The input's cards travel, the structural ones are the writer's own (once), and
    /// the HISTORY lines land at the end. Read back through the parser: BAYERPAT is
    /// there, NAXIS1 is the new size, and the raw cards show no duplicate keyword.
    /// </summary>
    [Fact]
    public void TheInputHeaderTravels_MinusTheStructuralCards_PlusHistory()
    {
        var (_, _, inputCards, _, _) = FitsReader.LoadFull(FitsTestData.BuildFits(64, 64, FitsTestData.Constant(1),
            extraCards: new[]
            {
                FitsTestData.Card("BAYERPAT", "'RGGB'"),
                FitsTestData.Card("STACKCNT", "30"),
                "COMMENT  keep me / with the slash".PadRight(80),
            }));

        byte[] fits = FitsWriter.Encode(Ramp(10, 8), -32, inputCards, new[] { "flatcheck test", "sursa: a.fit" });
        var (_, hdr, cards, _, _) = FitsReader.LoadFull(fits);

        Assert.Equal("RGGB", hdr["BAYERPAT"]);
        Assert.Equal("30", hdr["STACKCNT"]);
        Assert.Equal("10", hdr["NAXIS1"]);
        Assert.Equal("8", hdr["NAXIS2"]);
        Assert.Equal("-32", hdr["BITPIX"]);
        Assert.False(hdr.ContainsKey("BZERO"));
        Assert.Contains(cards, c => c.StartsWith("COMMENT  keep me / with the slash"));
        Assert.Contains(cards, c => c.StartsWith("HISTORY flatcheck test"));
        Assert.Contains(cards, c => c.StartsWith("HISTORY sursa: a.fit"));

        var keys = cards.Select(c => c.Substring(0, 8).Trim()).Where(k => k != "COMMENT" && k != "HISTORY" && k != "").ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        // the input's own structural cards (SIMPLE, BITPIX, NAXIS*, BZERO, BSCALE) were not copied
        // on top
        Assert.Single(keys, k => k == "SIMPLE");
        Assert.Single(keys, k => k == "BITPIX");
    }

    [Fact]
    public void TheFileIsMadeOf2880ByteBlocks_HeaderAndData()
    {
        byte[] fits = FitsWriter.Encode(Ramp(101, 37), -32, null, new[] { "x" });

        Assert.Equal(0, fits.Length % 2880);
        // header: 6 cards + HISTORY + END fits in one block; data 101*37*4 = 14948 -> 6 blocks
        Assert.Equal(2880 + 6 * 2880, fits.Length);
        Assert.StartsWith("SIMPLE  =                    T", Encoding.ASCII.GetString(fits, 0, 30));
    }

    [Fact]
    public void ALongHistoryLineIsWrappedOverSeveralCards()
    {
        string line = new string('a', 100) + new string('b', 50);

        var (_, _, cards, _, _) = FitsReader.LoadFull(FitsWriter.Encode(Ramp(2, 2), -32, null, new[] { line }));

        var history = cards.Where(c => c.StartsWith("HISTORY ")).Select(c => c.Substring(8).TrimEnd()).ToList();
        Assert.Equal(3, history.Count);
        Assert.Equal(line, string.Concat(history));
    }

    /// <summary>
    /// The writer streams: the same bytes come out of a Stream written in pieces as out
    /// of Encode, so the console can write 96 MB without a second copy in memory.
    /// </summary>
    [Fact]
    public void WriteToAStream_GivesTheSameBytesAsEncode()
    {
        Img img = Ramp(700, 500);   // 1.4 MB of float, more than one internal buffer

        using var ms = new MemoryStream();
        FitsWriter.Write(ms, img, -32, null, null);

        Assert.Equal(FitsWriter.Encode(img, -32, null, null), ms.ToArray());
    }
}
