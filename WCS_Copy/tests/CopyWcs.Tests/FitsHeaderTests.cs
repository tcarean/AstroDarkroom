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

    /// <summary>
    /// A file that is not FITS is refused at its first card, after its first block, however long
    /// it is, with the message that says so (R-015; CR-007 of the review of 2026-11-16: 200 MB of
    /// other bytes were read whole, 4.6 s and 546 MB more memory, then called truncated).
    /// </summary>
    [Fact]
    public void R015_ReadFrom_AFileThatIsNotFits_IsRefusedAtItsFirstCard()
    {
        byte[] text = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("A text file picked by mistake, not a FITS file.\n", 220_000)));
        Assert.True(text.Length > 10_000_000);
        using var stream = new MemoryStream(text);

        InvalidDataException ex;
        using (Ui.Use(UiLanguage.English))
        {
            ex = Assert.Throws<InvalidDataException>(() => FitsHeader.ReadFrom(stream));
        }

        Assert.Equal("The file does not begin with SIMPLE - it does not look like FITS.", ex.Message);
        Assert.True(stream.Position <= 2880, $"{stream.Position} bytes read");
    }

    /// <summary>
    /// A header that begins as FITS but holds no END in its first 1,000 blocks is refused there,
    /// with the END message, and nothing after those 2,880,000 bytes is read (R-015; CR-007): the
    /// owner's 5,200 FITS files of 2026-11-18 had headers of 1 to 4 blocks.
    /// </summary>
    [Fact]
    public void R015_ReadFrom_NoEndInTheFirst1000Blocks_IsRefusedThere()
    {
        var cards = new List<string> { FitsTestData.Card("SIMPLE", "T") };
        cards.AddRange(Enumerable.Range(1, 3000 * 36 - 1).Select(i => FitsTestData.Card("HISTORY", i.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        using var stream = new MemoryStream(FitsTestData.RawBytes(cards));
        Assert.Equal(3000 * 2880, stream.Length);

        InvalidDataException ex;
        using (Ui.Use(UiLanguage.English))
        {
            ex = Assert.Throws<InvalidDataException>(() => FitsHeader.ReadFrom(stream));
        }

        Assert.Equal("Truncated file, or not a valid FITS (the END card is missing).", ex.Message);
        Assert.Equal(1000 * 2880, stream.Position);
    }

    /// <summary>A header of exactly 1,000 blocks, its END the last card of the last one, is read whole (R-015): the limit is the 1,000th block, not one before it.</summary>
    [Fact]
    public void R015_ReadFrom_AHeaderOf1000Blocks_IsRead()
    {
        var cards = new List<string> { FitsTestData.Card("SIMPLE", "T") };
        cards.AddRange(Enumerable.Range(1, 1000 * 36 - 2).Select(i => FitsTestData.Card("HISTORY", i.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        byte[] bytes = FitsTestData.HeaderBlock(cards);
        Assert.Equal(1000 * 2880, bytes.Length);
        using var stream = new MemoryStream([.. bytes, .. FitsTestData.DeterministicData(400)]);

        var header = FitsHeader.ReadFrom(stream);

        Assert.Equal(1000 * 36 - 1, header.Keywords.Count());
        Assert.Equal(1000 * 2880, stream.Position);
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

    [Theory]
    [InlineData("-6.29608997961999E-06", -6.29608997961999E-06)]
    [InlineData("3.", 3.0)]
    [InlineData("1.0D-05", 1.0e-05)]
    [InlineData("1.0d-05", 1.0e-05)]
    [InlineData("  0.002  ", 0.002)]
    public void R017_GetDouble_ParsesAFitsNumber(string value, double expected)
    {
        var header = ReadHeader(FitsTestData.Card("X", value, "a comment"));

        Assert.Equal(expected, header.GetDouble("X")!.Value, 15);
    }

    [Theory]
    [InlineData("'text'")]
    [InlineData("T")]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void R017_GetDouble_AValueThatIsNotAFiniteNumber_IsNull(string value)
    {
        var header = ReadHeader(FitsTestData.Card("X", value));

        Assert.Null(header.GetDouble("X"));
        Assert.Null(header.GetDouble("MISSING"));
    }

    [Theory]
    [InlineData("'RA---TAN-SIP'", "RA---TAN-SIP")]
    [InlineData("'TAN     '", "TAN")]
    [InlineData("'it''s'", "it's")]
    [InlineData("'a/b' / a comment", "a/b")]
    [InlineData("3.", null)]
    [InlineData("'unclosed", null)]
    public void R017_GetString_ReadsAQuotedFitsString(string value, string? expected)
    {
        var header = ReadHeader(FitsTestData.Card("X", value));

        Assert.Equal(expected, header.GetString("X"));
        Assert.Null(header.GetString("MISSING"));
    }

    [Fact]
    public void R016_RemoveCards_RemovesEveryCardOfTheKeyword_AndReturnsThemInOrder()
    {
        string first = FitsTestData.Card("CD1_1", "-0.000264");
        string second = FitsTestData.Card("CD1_1", "-0.000265");
        var header = ReadHeader(FitsTestData.Card("A", "1"), first, FitsTestData.Card("B", "2"), second);

        IReadOnlyList<string> removed = header.RemoveCards("CD1_1");

        Assert.Equal([first, second], removed);
        Assert.False(header.Contains("CD1_1"));
        Assert.Equal(["SIMPLE", "A", "B"], header.Keywords);
    }

    [Fact]
    public void R016_RemoveCards_AKeywordTheHeaderLacks_RemovesNothing()
    {
        var header = ReadHeader(FitsTestData.Card("A", "1"));

        Assert.Empty(header.RemoveCards("CD1_1"));
        Assert.Equal(["SIMPLE", "A"], header.Keywords);
    }

    /// <summary>
    /// A keyword's cards, the CONTINUE cards under its first one with it, move up right above the
    /// first card the predicate accepts, the other cards keeping their order (R-061).
    /// </summary>
    [Fact]
    public void R061_MoveAbove_TheCardsGoRightAboveTheFirstCardAccepted_WithTheirContinueCards()
    {
        var header = ReadHeader(
            FitsTestData.Card("A", "1"),
            FitsTestData.Card("B", "2"),
            FitsTestData.Card("C", "3"),
            FitsTestData.Card("OBJECT", "'NGC 6960 &'"),
            FitsTestData.Continue("'Veil'"),
            FitsTestData.Card("D", "4"));

        header.MoveAbove("OBJECT", k => k is "B" or "C" or "OBJECT");

        Assert.Equal(["SIMPLE", "A", "OBJECT", "CONTINUE", "B", "C", "D"], header.Keywords);
        Assert.Equal([FitsTestData.Card("OBJECT", "'NGC 6960 &'"), FitsTestData.Continue("'Veil'")], header.ValueCards("OBJECT"));
    }

    /// <summary>Nothing moves when the keyword is missing, when no card the predicate accepts stands above it, or when it is itself the first accepted (R-061).</summary>
    [Fact]
    public void R061_MoveAbove_NothingAcceptedAboveTheCards_OrNoSuchKeyword_MovesNothing()
    {
        var header = ReadHeader(FitsTestData.Card("A", "1"), FitsTestData.Card("B", "2"), FitsTestData.Card("C", "3"));

        header.MoveAbove("B", k => k == "C");
        header.MoveAbove("B", k => k is "B" or "C");
        header.MoveAbove("Z", _ => true);

        Assert.Equal(["SIMPLE", "A", "B", "C"], header.Keywords);
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

    /// <summary>
    /// Only the space pads a keyword field or a value: the byte 0xA0, which Latin-1 reads as a
    /// no-break space that <c>string.Trim()</c> would strip, is part of the keyword, so a card
    /// whose field is <c>OBJECT</c> plus two of them is not the <c>OBJECT</c> card, one whose
    /// field is <c>END</c> plus one does not end the header, and a value ending in one is not an
    /// integer (the review of 2026-10-31; R-010's untouched card).
    /// </summary>
    [Fact]
    public void R010_ReadFrom_TheByte0xA0InAKeywordOrAValue_IsNotWhiteSpace()
    {
        var block = FitsTestData.HeaderBlock(new[]
        {
            FitsTestData.Card("SIMPLE", "T"),
            FitsTestData.Pad("OBJECT##= 'M43'"),
            FitsTestData.Pad("NAXIS1  = 100#"),
            // Last, so that a reader taking it for END (the old Trim()) still reads the two
            // cards above and each assertion fails on its own (the second review of 2026-10-31).
            FitsTestData.Pad("END#    = 'not the end'"),
        });
        for (var i = 0; i < block.Length; i++)
        {
            if (block[i] == (byte)'#')
            {
                block[i] = 0xA0;
            }
        }

        var header = FitsHeader.ReadFrom(new MemoryStream(block));

        Assert.Equal(new[] { "SIMPLE", "OBJECT" + (char)0xA0 + (char)0xA0, "NAXIS1", "END" + (char)0xA0 }, header.Keywords);
        Assert.False(header.Contains("OBJECT"));
        Assert.Equal(-1, header.GetInt("NAXIS1"));
    }

    /// <summary>
    /// The HISTORY card is ASCII: each letter with a diacritic is written as its base letter, the
    /// seven of R-011 by their code points so this file stays ASCII (ADR 0010).
    /// </summary>
    [Fact]
    public void R011_AddHistory_ALetterWithADiacritic_IsWrittenAsItsBaseLetter()
    {
        var header = ReadHeader();
        // a-breve, s-comma, t-comma, i-circumflex, a-circumflex, e-acute, A-ring.
        var text = "WCS copied from " + (char)0x0103 + (char)0x0219 + (char)0x021B + (char)0x00EE + (char)0x00E2 + (char)0x00E9 + (char)0x00C5 + ".fit";

        header.AddHistory(text);

        Assert.Equal("HISTORY WCS copied from astiaeA.fit".PadRight(80), header.GetCard("HISTORY"));
    }

    /// <summary>A character with no ASCII form (here an emoji, one character of two UTF-16 units) is one <c>?</c> in the card (ADR 0010).</summary>
    [Fact]
    public void R011_AddHistory_ACharacterWithNoAsciiForm_IsOneQuestionMark()
    {
        var header = ReadHeader();

        header.AddHistory("WCS copied from " + char.ConvertFromUtf32(0x1F30C) + ".fit");

        Assert.Equal("HISTORY WCS copied from ?.fit".PadRight(80), header.GetCard("HISTORY"));
    }

    /// <summary>The cut at 72 counts the ASCII characters written, after the transliteration (ADR 0010).</summary>
    [Fact]
    public void R011_AddHistory_TheCutAt72_CountsTheCharactersWritten()
    {
        var header = ReadHeader();

        header.AddHistory(new string((char)0x0103, 100));

        Assert.Equal("HISTORY " + new string('a', 72), header.GetCard("HISTORY"));
    }

    [Fact]
    public void Keywords_SkipsBlankCommentaryCards()
    {
        var blank = FitsTestData.Pad(new string(' ', 8) + "free-form commentary, no keyword");
        var header = ReadHeader(blank, FitsTestData.Card("OBJECT", "'M42'"));

        Assert.Equal(["SIMPLE", "OBJECT"], header.Keywords);
    }

    /// <summary>A keyword's value cards are its card and every CONTINUE card right under it, up to the next keyword; GetCard is the first alone (R-010).</summary>
    [Fact]
    public void R010_ValueCards_AreTheCardAndTheContinueCardsRightUnderIt()
    {
        var first = FitsTestData.Card("OBJECT", "'NGC 6960 &'");
        var second = FitsTestData.Continue("'Veil &'");
        var third = FitsTestData.Continue("'Nebula'");
        var header = ReadHeader(first, second, third, FitsTestData.Card("TELESCOP", "'72ED'"), FitsTestData.Continue("'stray'"));

        Assert.Equal([first, second, third], header.ValueCards("OBJECT"));
        Assert.Equal(first, header.GetCard("OBJECT"));
        Assert.Equal(2, header.ValueCards("TELESCOP").Count);
        Assert.Empty(header.ValueCards("INSTRUME"));
    }

    /// <summary>Setting a value replaces its card and the CONTINUE cards under it where they stand, the cards after them in place; a keyword the header lacks is appended whole (R-010).</summary>
    [Fact]
    public void R010_SetValueCards_ReplacesTheCardAndItsContinueCardsWhereTheyStand()
    {
        var header = ReadHeader(FitsTestData.Card("OBJECT", "'old &'"), FitsTestData.Continue("'tail'"), FitsTestData.Card("SITELAT", "46.77"));
        string[] longValue = [FitsTestData.Card("TELESCOP", "'a &'"), FitsTestData.Continue("'b'")];

        header.SetValueCards("OBJECT", [FitsTestData.Card("OBJECT", "'M42'")]);
        header.SetValueCards("TELESCOP", longValue);

        Assert.Equal(["SIMPLE", "OBJECT", "SITELAT", "TELESCOP", "CONTINUE"], header.Keywords);
        Assert.Equal(FitsTestData.Card("OBJECT", "'M42'"), header.GetCard("OBJECT"));
        Assert.Equal(longValue, header.ValueCards("TELESCOP"));
        Assert.Throws<ArgumentOutOfRangeException>(() => header.SetValueCards("OBJECT", []));
    }

    /// <summary>Removing a keyword takes each of its cards with the CONTINUE cards right under it, and CardsOf lists those same cards first (R-016, R-010).</summary>
    [Fact]
    public void R016_RemoveCards_TakesEachCardWithItsContinueCards()
    {
        var header = ReadHeader(FitsTestData.Card("CTYPE1", "'RA---TAN &'"), FitsTestData.Continue("'x'"),
            FitsTestData.Card("OBJECT", "'M42'"), FitsTestData.Card("CTYPE1", "'second'"));

        var listed = header.CardsOf("CTYPE1");
        var removed = header.RemoveCards("CTYPE1");

        Assert.Equal(3, removed.Count);
        Assert.Equal(listed, removed);
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

        // The temporary file has a name of its own (R-012): the folder holds the destination alone.
        Assert.Equal(["dest.fit"], Directory.GetFiles(ws.Dir).Select(f => Path.GetFileName(f)));

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
