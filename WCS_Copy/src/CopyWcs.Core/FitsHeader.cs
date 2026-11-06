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
using NLog;
using static CopyWcs.Ui;

namespace CopyWcs;

/// <summary>
/// The header of the primary HDU, as a list of raw 80-character cards, read and written by
/// hand: no FITS library (ADR 0001). Each card is the file's bytes one to one, through Latin-1,
/// so a byte above 0x7F comes back as it was read (R-010, ADR 0010). How the cards, the blocks
/// and the in-place write fit together is in <c>docs/design/fits-header.md</c>; the lines it
/// writes to the diagnostic log, in <c>docs/design/logging.md</c>.
/// </summary>
internal sealed class FitsHeader
{
    private const int CardSize = 80;
    private const int BlockSize = 2880;

    /// <summary>
    /// The encoding of the cards: each byte 0x00 to 0xFF is the character of that code, and
    /// each such character is written back as the same byte, so no byte of a header is changed
    /// by the round trip (R-010, ADR 0010); an all-ASCII header reads and writes as under ASCII.
    /// </summary>
    private static readonly Encoding CardEncoding = Encoding.Latin1;

    /// <summary>The header engine's lines in the diagnostic log: the reads, the writes and, at Trace, every card (R-042, R-044).</summary>
    private static readonly Logger Log = LogManager.GetLogger("FitsHeader");

    private readonly List<string> _cards = new();

    /// <summary>The offset in the file where the data area begins, after the padded header.</summary>
    private long _dataOffset;

    /// <summary>The keyword of every card that has one, in the header's order; blank cards are left out.</summary>
    public IEnumerable<string> Keywords => _cards.Select(KeywordOf).Where(k => k.Length > 0);

    /// <summary>
    /// Reads the primary header of a FITS file, and writes its read line to the diagnostic log:
    /// the file's size, the header's cards, blocks and data offset, and the keys that say what
    /// the image is (R-042); at Trace every card (R-044). A file that cannot be read throws, and
    /// its caller logs the exception.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The header, with the data offset remembered for <see cref="WriteWithDataFrom"/>.</returns>
    /// <exception cref="InvalidDataException">The file does not begin with SIMPLE, or ends before an END card.</exception>
    /// <exception cref="IOException">The file cannot be opened or read (missing, a folder in the path missing, locked).</exception>
    /// <exception cref="UnauthorizedAccessException">The path is a directory, or the file cannot be read by this user.</exception>
    /// <exception cref="ArgumentException">The path is empty or blank.</exception>
    public static FitsHeader ReadPrimary(string path)
    {
        using var fs = File.OpenRead(path);
        var h = ReadFrom(fs);

        long? length = LengthOf(fs);
        Log.Info("read " + path + " (" + (length is long n ? LogText.Size(n) : "size unknown") + ") bytes=" + LogText.Int(length)
                 + " cards=" + LogText.Int(h._cards.Count) + " blocks=" + LogText.Int(h._dataOffset / BlockSize)
                 + " dataOffset=" + LogText.Int(h._dataOffset) + " data=" + LogText.Int(length - h._dataOffset)
                 + " " + LogText.Keys(h));
        h.TraceCards(path);
        return h;
    }

    /// <summary>
    /// A stream's length for the read line, or null when it cannot tell: a pipe or a device
    /// cannot seek, and asking would throw; a seekable file may throw too, an
    /// <see cref="IOException"/> for a share that dropped after the header was read, an
    /// <see cref="UnauthorizedAccessException"/> for a query the system denies. The line then
    /// says <c>size unknown</c>: the header is read, and the log never stops a run (R-041).
    /// </summary>
    /// <param name="stream">The stream the header was read from.</param>
    /// <returns>The length in bytes, or null when the stream cannot seek or the query throws.</returns>
    internal static long? LengthOf(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : null;
        }
        catch (Exception)
        {
            // Whatever the query throws: a value only the log reads must never stop the copy
            // (ADR 0009), and the header it would describe is already read.
            return null;
        }
    }

    /// <summary>Writes every card of this header to the diagnostic log at Trace, numbered from 1; nothing when Trace is off (R-044).</summary>
    /// <param name="path">The file the cards were read from or are written to, the start of each line.</param>
    private void TraceCards(string path)
    {
        if (!Log.IsTraceEnabled)
        {
            return;
        }

        for (var i = 0; i < _cards.Count; i++)
        {
            Log.Trace(path + " card " + LogText.Int(i + 1) + ": " + LogText.Card(_cards[i]));
        }
    }

    /// <summary>
    /// Reads a primary header from the start of a stream: whole 2880-byte blocks, card by
    /// card, up to and including the block that holds END; each card's bytes become its
    /// characters one to one (<see cref="CardEncoding"/>).
    /// </summary>
    /// <param name="stream">The stream, positioned at the first byte of the file; read in full blocks.</param>
    /// <returns>The header; the stream is left after the last header block.</returns>
    /// <exception cref="InvalidDataException">The stream ends before an END card, or its first card is not SIMPLE.</exception>
    public static FitsHeader ReadFrom(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var h = new FitsHeader();

        var block = new byte[BlockSize];
        var sawEnd = false;
        long consumed = 0;

        while (!sawEnd)
        {
            var read = ReadFully(stream, block);
            if (read < BlockSize)
            {
                throw new InvalidDataException(T("Truncated file, or not a valid FITS (the END card is missing).",
                    "Fisier trunchiat sau nu este FITS valid (cardul END lipseste)."));
            }

            consumed += BlockSize;

            for (var i = 0; i < BlockSize; i += CardSize)
            {
                var card = CardEncoding.GetString(block, i, CardSize);
                if (KeywordOf(card) == "END")
                {
                    sawEnd = true;
                    break;
                }
                h._cards.Add(card);
            }
        }

        if (h._cards.Count == 0 || KeywordOf(h._cards[0]) != "SIMPLE")
        {
            throw new InvalidDataException(T("The file does not begin with SIMPLE - it does not look like FITS.",
                "Fisierul nu incepe cu SIMPLE - nu pare FITS."));
        }

        h._dataOffset = consumed;
        return h;
    }

    /// <summary>
    /// Reads until the buffer is full or the stream ends: <see cref="Stream.Read(byte[], int, int)"/>
    /// may return fewer bytes than asked, even in the middle of the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The number of bytes read; less than the buffer's length only at the end of the stream.</returns>
    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }
        return total;
    }

    /// <summary>
    /// The keyword of a card: its first eight characters, trimmed of spaces only, the one
    /// character FITS pads with; the byte 0xA0, a no-break space in Latin-1, and a control
    /// character 0x09 to 0x0D are part of the keyword, so that no card with such a byte passes
    /// for another (R-010).
    /// </summary>
    /// <param name="card">The card; shorter than eight characters means no keyword.</param>
    /// <returns>The keyword, or an empty string for a blank or short card.</returns>
    private static string KeywordOf(string card) =>
        card.Length < 8 ? string.Empty : card[..8].Trim(' ');

    /// <summary>Whether a card with this keyword is in the header.</summary>
    /// <param name="key">The keyword, as written in the card, case-sensitive.</param>
    /// <returns>True when a card has it.</returns>
    public bool Contains(string key) => IndexOf(key) >= 0;

    /// <summary>The index of the first card with this keyword.</summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The index, or -1 when no card has it.</returns>
    private int IndexOf(string key)
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            if (string.Equals(KeywordOf(_cards[i]), key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The raw card of a keyword, exactly as read or set.</summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The 80-character card, or null when no card has the keyword.</returns>
    public string? GetCard(string key)
    {
        var i = IndexOf(key);
        return i < 0 ? null : _cards[i];
    }

    /// <summary>The integer value of a card: the text between the '=' and the '/' comment, if any, trimmed of spaces only.</summary>
    /// <param name="key">The keyword, case-sensitive; safe for NAXIS1 and the other integer keys.</param>
    /// <returns>The value, or -1 when the card is missing, has no '=' or does not hold an integer (a byte 0xA0 beside the digits is not an integer).</returns>
    public int GetInt(string key)
    {
        var card = GetCard(key);
        if (card is null)
        {
            return -1;
        }

        var eq = card.IndexOf('=');
        if (eq < 0)
        {
            return -1;
        }

        var value = card[(eq + 1)..];
        var slash = value.IndexOf('/');
        if (slash >= 0)
        {
            value = value[..slash];
        }

        return int.TryParse(value.Trim(' '), out var n) ? n : -1;
    }

    /// <summary>Replaces the card of a keyword in place, or appends it when the header has none.</summary>
    /// <param name="key">The keyword the card is filed under, case-sensitive.</param>
    /// <param name="rawCard">The card's text, each character one byte of the file (a card read from a file, normally); padded to 80 characters, or cut to 80 when longer. A character above 0xFF has no byte and is written as <c>?</c>.</param>
    public void SetCard(string key, string rawCard)
    {
        var card = Pad(rawCard);
        var i = IndexOf(key);
        if (i >= 0)
        {
            _cards[i] = card;
        }
        else
        {
            _cards.Add(card);
        }
    }

    /// <summary>
    /// Removes every card of a keyword, wherever it sits, the other cards keeping their order:
    /// how a copy drops the destination's old plate-solve cards (R-016).
    /// </summary>
    /// <param name="key">The keyword, case-sensitive, as <see cref="Contains"/> takes it.</param>
    /// <returns>The cards removed, raw, in the order the header held them; empty when no card had the keyword.</returns>
    public IReadOnlyList<string> RemoveCards(string key)
    {
        var removed = CardsOf(key);
        if (removed.Count > 0)
        {
            _cards.RemoveAll(c => string.Equals(KeywordOf(c), key, StringComparison.Ordinal));
        }

        return removed;
    }

    /// <summary>Every card of a keyword, raw, in the order the header holds them; a header may hold a keyword more than once.</summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The cards; empty when no card has the keyword.</returns>
    public IReadOnlyList<string> CardsOf(string key) =>
        _cards.Where(c => string.Equals(KeywordOf(c), key, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// Appends a HISTORY card, ASCII whatever the text holds (<see cref="AsciiText.Of"/>), so the
    /// copy never puts a byte above 0x7F in a header itself, and writes it to the diagnostic log
    /// at Debug as it was stored (R-011, R-042, ADR 0010).
    /// </summary>
    /// <param name="text">The text after the keyword; made ASCII, then cut to 72 characters, the room a card has after "HISTORY ".</param>
    public void AddHistory(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ascii = AsciiText.Of(text);
        var body = ascii.Length > 72 ? ascii[..72] : ascii;
        var card = Pad("HISTORY " + body);
        _cards.Add(card);
        Log.Debug("appended " + LogText.Card(card));
    }

    /// <summary>A card at exactly 80 characters: padded with spaces, or cut.</summary>
    /// <param name="card">The card's text.</param>
    /// <returns>The 80-character card.</returns>
    private static string Pad(string card) =>
        card.Length >= CardSize ? card[..CardSize] : card.PadRight(CardSize);

    /// <summary>
    /// Writes this header, END and the padding to the next 2880-byte boundary, followed by
    /// the data area copied from <paramref name="dataSource"/>. When the target is the data
    /// source itself, the file is written beside it first and moved over it at the end: the
    /// header may have grown past its original blocks, so it cannot be patched in place. Each
    /// card's characters are written as the bytes they were read from (<see cref="CardEncoding"/>).
    /// The diagnostic log gets the write before it starts (the temporary file of an in-place write;
    /// at Trace every card to be written, so a failed write still shows the header it meant to
    /// write) and the file written after it (R-042, R-044); a write that throws is its caller's
    /// to log.
    /// </summary>
    /// <param name="dataSource">The FITS file whose data area, from its own header's end, is copied; normally the file this header was read from.</param>
    /// <param name="target">The file to write; the same path as <paramref name="dataSource"/> for an in-place rewrite.</param>
    /// <exception cref="IOException">The target cannot be created or written.</exception>
    /// <exception cref="UnauthorizedAccessException">The target, or the original of an in-place rewrite, cannot be replaced by this user.</exception>
    /// <exception cref="ArgumentException">A path is empty or blank.</exception>
    public void WriteWithDataFrom(string dataSource, string target)
    {
        var inPlace = Path.GetFullPath(dataSource) == Path.GetFullPath(target);
        var temp = inPlace ? target + ".tmp" : target;

        Log.Debug("write " + target + (inPlace ? " in place through " + temp : "")
                  + ", the data from byte " + LogText.Int(_dataOffset) + " of " + dataSource);
        TraceCards(target);

        long headerBytes;
        long total;
        using (var input = File.OpenRead(dataSource))
        using (var output = File.Create(temp))
        {
            foreach (var card in _cards)
            {
                output.Write(CardEncoding.GetBytes(Pad(card)));
            }

            output.Write(CardEncoding.GetBytes("END".PadRight(CardSize)));

            // Spaces up to the next 2880-byte boundary.
            var remainder = (int)(output.Position % BlockSize);
            if (remainder != 0)
            {
                var fill = new byte[BlockSize - remainder];
                Array.Fill(fill, (byte)' ');
                output.Write(fill);
            }

            headerBytes = output.Position;
            input.Seek(_dataOffset, SeekOrigin.Begin);
            input.CopyTo(output);
            total = output.Position;
        }

        if (inPlace)
        {
            File.Move(temp, target, overwrite: true);
        }

        Log.Info("wrote " + target + " (" + LogText.Size(total) + ") bytes=" + LogText.Int(total)
                 + " cards=" + LogText.Int(_cards.Count) + " blocks=" + LogText.Int(headerBytes / BlockSize)
                 + " data=" + LogText.Int(total - headerBytes));
    }
}
