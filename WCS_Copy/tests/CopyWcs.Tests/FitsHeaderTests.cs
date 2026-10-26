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

namespace CopyWcs.Tests;

public class FitsHeaderTests
{
    private static FitsHeader ReadHeader(params string[] extraCards)
    {
        var cards = new List<string> { FitsTestData.Card("SIMPLE", "T") };
        cards.AddRange(extraCards);
        return FitsHeader.ReadFrom(new MemoryStream(FitsTestData.HeaderBlock(cards)));
    }

    [Fact]
    public void ReadFrom_ValidMinimalHeader_ParsesKeywords()
    {
        var bytes = FitsTestData.HeaderBlock(new[]
        {
            FitsTestData.Card("SIMPLE", "T"),
            FitsTestData.Card("BITPIX", "16"),
            FitsTestData.Card("NAXIS", "0"),
            FitsTestData.Card("CTYPE1", "'RA---TAN'"),
        });

        var header = FitsHeader.ReadFrom(new MemoryStream(bytes));

        Assert.Equal(["SIMPLE", "BITPIX", "NAXIS", "CTYPE1"], header.Keywords);
        Assert.True(header.Contains("CTYPE1"));
        Assert.False(header.Contains("CTYPE2"));
    }

    [Fact]
    public void R015_ReadFrom_MissingSimpleAsFirstCard_Throws()
    {
        var bytes = FitsTestData.HeaderBlock(new[]
        {
            FitsTestData.Card("BITPIX", "16"),
            FitsTestData.Card("NAXIS", "0"),
        });

        Assert.Throws<InvalidDataException>(() => FitsHeader.ReadFrom(new MemoryStream(bytes)));
    }

    [Fact]
    public void R015_ReadFrom_EmptyHeaderJustEnd_Throws()
    {
        var bytes = FitsTestData.HeaderBlock(Array.Empty<string>());

        Assert.Throws<InvalidDataException>(() => FitsHeader.ReadFrom(new MemoryStream(bytes)));
    }

    [Fact]
    public void R015_ReadFrom_TruncatedShortStream_Throws()
    {
        // A single card, far under one 2880-byte block, and no END.
        var bytes = FitsTestData.RawBytes(new[] { FitsTestData.Card("SIMPLE", "T") });

        Assert.Throws<InvalidDataException>(() => FitsHeader.ReadFrom(new MemoryStream(bytes)));
    }

    [Fact]
    public void R015_ReadFrom_ExactBlockMultipleButNoEnd_Throws()
    {
        // Exactly 36 cards (= 2880 bytes, one full block), no END anywhere:
        // the first block reads fine, but looking for the next block hits EOF.
        var cards = Enumerable.Range(0, 36).Select(i => FitsTestData.Card($"KEY{i:00}", i.ToString()));
        var bytes = FitsTestData.RawBytes(cards);
        Assert.Equal(2880, bytes.Length);

        Assert.Throws<InvalidDataException>(() => FitsHeader.ReadFrom(new MemoryStream(bytes)));
    }

    [Fact]
    public void ReadFrom_HeaderSpanningMultipleBlocks_ReadsAllCards()
    {
        var cards = new List<string> { FitsTestData.Card("SIMPLE", "T") };
        cards.AddRange(Enumerable.Range(0, 40).Select(i => FitsTestData.Card($"KEY{i:00}", i.ToString())));

        var bytes = FitsTestData.HeaderBlock(cards);
        Assert.True(bytes.Length > 2880);

        var header = FitsHeader.ReadFrom(new MemoryStream(bytes));

        Assert.Equal(cards.Count, header.Keywords.Count());
        Assert.True(header.Contains("KEY39"));
    }

    [Fact]
    public void ReadFrom_SlowStreamReturningShortReads_StillParsesCorrectly()
    {
        var cards = new List<string> { FitsTestData.Card("SIMPLE", "T") };
        cards.AddRange(Enumerable.Range(0, 40).Select(i => FitsTestData.Card($"KEY{i:00}", i.ToString())));
        var bytes = FitsTestData.HeaderBlock(cards);

        var header = FitsHeader.ReadFrom(new DribbleStream(bytes, maxPerRead: 3));

        Assert.Equal(cards.Count, header.Keywords.Count());
        Assert.True(header.Contains("KEY39"));
    }

    [Fact]
    public void GetCard_KeyPresent_ReturnsRawCardText()
    {
        var card = FitsTestData.Card("OBJECT", "'M42'", "target name");
        var header = ReadHeader(card);

        Assert.Equal(card, header.GetCard("OBJECT"));
    }

    [Fact]
    public void GetCard_KeyMissing_ReturnsNull()
    {
        var header = ReadHeader();

        Assert.Null(header.GetCard("OBJECT"));
    }

    [Theory]
    [InlineData("1024", null, 1024)]
    [InlineData("1024", "width in px", 1024)]
    [InlineData("not-a-number", null, -1)]
    public void GetInt_ParsesOrFallsBackToNegativeOne(string value, string? comment, int expected)
    {
        var header = ReadHeader(FitsTestData.Card("NAXIS1", value, comment));

        Assert.Equal(expected, header.GetInt("NAXIS1"));
    }

    [Fact]
    public void GetInt_MissingKey_ReturnsNegativeOne()
    {
        var header = ReadHeader();

        Assert.Equal(-1, header.GetInt("NAXIS1"));
    }

    [Fact]
    public void GetInt_CardWithNoEqualsSign_ReturnsNegativeOne()
    {
        // HISTORY and COMMENT cards have no '=' in column 9.
        var header = ReadHeader(FitsTestData.Pad("HISTORY something happened"));

        Assert.Equal(-1, header.GetInt("HISTORY"));
    }

    [Fact]
    public void SetCard_KeyExists_ReplacesInPlacePreservingPosition()
    {
        var header = ReadHeader(
            FitsTestData.Card("A", "1"), FitsTestData.Card("B", "2"), FitsTestData.Card("C", "3"));

        header.SetCard("B", FitsTestData.Card("B", "999"));

        Assert.Equal(["SIMPLE", "A", "B", "C"], header.Keywords);
        Assert.Contains("999", header.GetCard("B"));
    }

    [Fact]
    public void SetCard_KeyAbsent_AppendsAtEnd()
    {
        var header = ReadHeader(FitsTestData.Card("A", "1"), FitsTestData.Card("B", "2"));

        header.SetCard("Z", FitsTestData.Card("Z", "42"));

        Assert.Equal(["SIMPLE", "A", "B", "Z"], header.Keywords);
        Assert.True(header.Contains("Z"));
    }

    [Fact]
    public void SetCard_ShortCard_PadsTo80Chars()
    {
        var header = ReadHeader();

        header.SetCard("X", FitsTestData.UnpaddedCard("X", "1"));

        var card = header.GetCard("X");
        Assert.NotNull(card);
        Assert.Equal(80, card!.Length);
    }

    [Fact]
    public void SetCard_OverlongCard_TruncatesTo80Chars()
    {
        var header = ReadHeader();
        var longRaw = FitsTestData.UnpaddedCard("X", new string('9', 200));
        Assert.True(longRaw.Length > 80);

        header.SetCard("X", longRaw);

        var card = header.GetCard("X");
        Assert.NotNull(card);
        Assert.Equal(80, card!.Length);
        Assert.Equal(longRaw[..80], card);
    }

    [Fact]
    public void R011_AddHistory_TextExactly72Chars_FillsCardWithNoPadding()
    {
        var header = ReadHeader();
        var text = new string('x', 72);

        header.AddHistory(text);

        Assert.Equal("HISTORY " + text, header.GetCard("HISTORY"));
    }

    [Fact]
    public void R011_AddHistory_TextLongerThan72Chars_TruncatesBody()
    {
        var header = ReadHeader();

        header.AddHistory(new string('x', 100));

        Assert.Equal("HISTORY " + new string('x', 72), header.GetCard("HISTORY"));
    }

    [Fact]
    public void R011_AddHistory_TextShorterThan72Chars_PadsWithSpaces()
    {
        var header = ReadHeader();

        header.AddHistory("short note");

        Assert.Equal("HISTORY short note".PadRight(80), header.GetCard("HISTORY"));
    }

    [Fact]
    public void Keywords_SkipsBlankCommentaryCards()
    {
        var blank = FitsTestData.Pad(new string(' ', 8) + "free-form commentary, no keyword");
        var header = ReadHeader(blank, FitsTestData.Card("OBJECT", "'M42'"));

        Assert.Equal(["SIMPLE", "OBJECT"], header.Keywords);
    }

    [Fact]
    public void R013_WriteWithDataFrom_NewOutputFile_HeaderWrittenAndDataBytesPreserved()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(500);
        var sourcePath = ws.WriteFile("source.fit", FitsTestData.MinimalFitsFile(
            new[] { FitsTestData.Card("SIMPLE", "T"), FitsTestData.Card("OBJECT", "'orig'") }, data));
        var targetPath = ws.FilePath("output.fit");

        var header = FitsHeader.ReadPrimary(sourcePath);
        header.SetCard("OBJECT", FitsTestData.Card("OBJECT", "'changed'"));
        header.WriteWithDataFrom(sourcePath, targetPath);

        var rewritten = FitsHeader.ReadPrimary(targetPath);
        Assert.Contains("changed", rewritten.GetCard("OBJECT"));

        var targetBytes = File.ReadAllBytes(targetPath);
        Assert.Equal(2880 + data.Length, targetBytes.Length);
        Assert.Equal(data, targetBytes[^data.Length..]);
    }

    [Fact]
    public void R012_WriteWithDataFrom_InPlaceOverwrite_NoLeftoverTempFileAndDataPreserved()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(300);
        var path = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(
            new[] { FitsTestData.Card("SIMPLE", "T"), FitsTestData.Card("OBJECT", "'orig'") }, data));

        var header = FitsHeader.ReadPrimary(path);
        header.SetCard("OBJECT", FitsTestData.Card("OBJECT", "'changed'"));
        header.WriteWithDataFrom(path, path);

        Assert.False(File.Exists(path + ".tmp"));

        var rewritten = FitsHeader.ReadPrimary(path);
        Assert.Contains("changed", rewritten.GetCard("OBJECT"));

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(data, bytes[^data.Length..]);
    }

    [Fact]
    public void R013_WriteWithDataFrom_HeaderGrowsPastOriginalBlockCount_PadsCorrectlyAndPreservesData()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(200);
        var path = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(
            new[] { FitsTestData.Card("SIMPLE", "T") }, data));

        var header = FitsHeader.ReadPrimary(path);
        // SIMPLE + 40 keys > 36 per block: the header must grow to 2 blocks.
        for (var i = 0; i < 40; i++)
        {
            header.SetCard($"KEY{i:00}", FitsTestData.Card($"KEY{i:00}", i.ToString()));
        }

        var outPath = ws.FilePath("grown.fit");
        header.WriteWithDataFrom(path, outPath);

        var bytes = File.ReadAllBytes(outPath);
        Assert.Equal(2 * 2880 + data.Length, bytes.Length);

        var reread = FitsHeader.ReadPrimary(outPath);
        Assert.True(reread.Contains("KEY39"));
        Assert.Equal(data, bytes[^data.Length..]);
    }

    /// <summary>A fake stream that returns only a few bytes per call, to check that ReadFrom does not assume one Read() fills the buffer.</summary>
    private sealed class DribbleStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _maxPerRead;

        public DribbleStream(byte[] data, int maxPerRead)
        {
            _inner = new MemoryStream(data);
            _maxPerRead = maxPerRead;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, Math.Min(count, _maxPerRead));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
