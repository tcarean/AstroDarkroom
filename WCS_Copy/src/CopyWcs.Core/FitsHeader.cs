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

using System.Globalization;
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

    /// <summary>The keyword of the cards that carry on a long string value from the card above them (R-010).</summary>
    private const string ContinueKeyword = "CONTINUE";

    /// <summary>The key under which a failed write keeps its temporary file's full path in the exception's <see cref="Exception.Data"/> (R-014).</summary>
    private const string TemporaryKey = "CopyWcs.TemporaryFile";

    /// <summary>The key under which a failed write keeps its target's full path in the exception's <see cref="Exception.Data"/> (R-014).</summary>
    private const string TargetKey = "CopyWcs.Target";

    /// <summary>The key under which a failed write keeps the backup of the old target it could not put back, in the exception's <see cref="Exception.Data"/> (R-014).</summary>
    private const string KeptKey = "CopyWcs.KeptBackup";

    /// <summary>
    /// Stands in for Windows' replace in a test, on the test's own thread: the temporary file,
    /// the target and the backup name, as <see cref="File.Replace(string, string, string?, bool)"/>
    /// takes them; null, the real replace. Windows' halfway failures (1176, 1177) cannot be made
    /// on demand, so a test makes them through this (ADR 0031).
    /// </summary>
    [ThreadStatic]
    internal static Action<string, string, string>? ReplaceForTests;

    /// <summary>The longest file name NTFS, FAT and exFAT accept, in UTF-16 units: the limit a write's temporary name keeps within (R-012).</summary>
    private const int NameLimit = 255;

    /// <summary>
    /// The most blocks the reader reads looking for END (R-015, ADR 0024): 1,000 blocks, 2,880,000
    /// bytes, where the owner's 5,200 FITS files of 2026-11-18 had headers of 1 to 4 blocks, so a
    /// file that begins as FITS but never ends its header is not read to its end.
    /// </summary>
    private const int MaxHeaderBlocks = 1000;

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
    /// <exception cref="InvalidDataException">The file does not begin with SIMPLE, or ends before an END card, or holds none in its first 1,000 blocks (<see cref="ReadFrom"/>).</exception>
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
    /// characters one to one (<see cref="CardEncoding"/>). A file that is not FITS is refused at
    /// its first card, after its first block, and a header is looked through for END over
    /// <see cref="MaxHeaderBlocks"/> blocks at most, so no file is read to its end because it is
    /// not a FITS header (R-015).
    /// </summary>
    /// <param name="stream">The stream, positioned at the first byte of the file; read in full blocks.</param>
    /// <returns>The header; the stream is left after the last header block.</returns>
    /// <exception cref="InvalidDataException">The first card is not SIMPLE; or the stream ends before an END card, or holds none in its first <see cref="MaxHeaderBlocks"/> blocks.</exception>
    public static FitsHeader ReadFrom(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var h = new FitsHeader();

        var block = new byte[BlockSize];
        var sawEnd = false;
        long consumed = 0;

        while (!sawEnd)
        {
            if (consumed == (long)MaxHeaderBlocks * BlockSize)
            {
                throw NoEnd();
            }

            var read = ReadFully(stream, block);

            // The first card says whether the file is FITS at all: one that is not, a TIFF or a
            // video picked by mistake, is refused here, before a byte more is read (CR-007 of the
            // review of 2026-11-16). A file shorter than one card has none, and reads as cut short.
            if (consumed == 0 && read >= CardSize && KeywordOf(CardEncoding.GetString(block, 0, CardSize)) != "SIMPLE")
            {
                throw new InvalidDataException(T("The file does not begin with SIMPLE - it does not look like FITS.",
                    "Fisierul nu incepe cu SIMPLE - nu pare FITS."));
            }

            if (read < BlockSize)
            {
                throw NoEnd();
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

        h._dataOffset = consumed;
        return h;
    }

    /// <summary>The reader's refusal of a header without its END card: the stream ended first, or <see cref="MaxHeaderBlocks"/> blocks held none (R-015).</summary>
    /// <returns>The exception to throw, its message in the run's language.</returns>
    private static InvalidDataException NoEnd()
        => new(T("Truncated file, or not a valid FITS (the END card is missing).",
            "Fisier trunchiat sau nu este FITS valid (cardul END lipseste)."));

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

    /// <summary>The raw card of a keyword, exactly as read or set: its first card alone, without the CONTINUE cards of a long string (<see cref="ValueCards"/>).</summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The 80-character card, or null when no card has the keyword.</returns>
    public string? GetCard(string key)
    {
        var i = IndexOf(key);
        return i < 0 ? null : _cards[i];
    }

    /// <summary>
    /// The cards that hold a keyword's value: its first card and the CONTINUE cards right under
    /// it, which carry on a long string (R-010), raw and in their order; a value of one card is
    /// that card alone.
    /// </summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The cards, the keyword's own first; empty when no card has the keyword.</returns>
    public IReadOnlyList<string> ValueCards(string key)
    {
        var i = IndexOf(key);
        return i < 0 ? [] : _cards.GetRange(i, 1 + ContinuationsUnder(i));
    }

    /// <summary>How many CONTINUE cards stand right under a card, up to the first card of another keyword.</summary>
    /// <param name="i">The card's index in the header.</param>
    /// <returns>The number of CONTINUE cards that follow it without a gap; 0 for a value of one card.</returns>
    private int ContinuationsUnder(int i)
    {
        var n = 0;
        while (i + 1 + n < _cards.Count && KeywordOf(_cards[i + 1 + n]) == ContinueKeyword)
        {
            n++;
        }

        return n;
    }

    /// <summary>The integer value of a card: the text between the '=' and the '/' comment, if any, trimmed of spaces only.</summary>
    /// <param name="key">The keyword, case-sensitive; safe for NAXIS1 and the other integer keys.</param>
    /// <returns>The value, or -1 when the card is missing, has no '=' or does not hold an integer (a byte 0xA0 beside the digits is not an integer).</returns>
    public int GetInt(string key)
    {
        var value = ValueText(key);
        return value is not null && int.TryParse(value, out var n) ? n : -1;
    }

    /// <summary>
    /// The number a card holds, read like <see cref="GetInt"/>, the FITS exponent letter <c>D</c>
    /// taken as <c>E</c> (<c>1.0D-05</c>): the CD, CDELT and PC values whose determinant R-017
    /// compares. Invariant whatever the culture.
    /// </summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The value; null when the card is missing, has no '=' or does not hold a finite number (a string, a logical, nothing).</returns>
    public double? GetDouble(string key)
    {
        var value = ValueText(key)?.Replace('D', 'E').Replace('d', 'e');
        return value is not null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x)
            ? x
            : null;
    }

    /// <summary>
    /// The text a card's string value holds, between its quotes, a doubled quote read as one and
    /// the trailing spaces dropped (<c>'TAN     '</c> gives <c>TAN</c>): the <c>CTYPE1</c> whose
    /// axis R-017 compares. A '/' inside the quotes is part of the text.
    /// </summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The text; null when the card is missing, has no '=', or its value is not a quoted string closed on the card.</returns>
    public string? GetString(string key)
    {
        var card = GetCard(key);
        var eq = card?.IndexOf('=') ?? -1;
        if (card is null || eq < 0)
        {
            return null;
        }

        var rest = card[(eq + 1)..].TrimStart(' ');
        if (rest.Length == 0 || rest[0] != '\'')
        {
            return null;
        }

        var text = new StringBuilder();
        for (var i = 1; i < rest.Length; i++)
        {
            if (rest[i] != '\'')
            {
                text.Append(rest[i]);
            }
            else if (i + 1 < rest.Length && rest[i + 1] == '\'')
            {
                text.Append('\'');
                i++;
            }
            else
            {
                return text.ToString().TrimEnd(' ');
            }
        }

        return null;
    }

    /// <summary>The text of a card's value: between the '=' and the '/' of a comment, if any, trimmed of spaces only.</summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The text; null when the card is missing or has no '='.</returns>
    private string? ValueText(string key)
    {
        var card = GetCard(key);
        var eq = card?.IndexOf('=') ?? -1;
        if (card is null || eq < 0)
        {
            return null;
        }

        var value = card[(eq + 1)..];
        var slash = value.IndexOf('/');
        return (slash >= 0 ? value[..slash] : value).Trim(' ');
    }

    /// <summary>Replaces the value of a keyword with one card, where its first card stands, the CONTINUE cards under that card going with it, or appends the card when the header has none.</summary>
    /// <param name="key">The keyword the card is filed under, case-sensitive.</param>
    /// <param name="rawCard">The card's text, each character one byte of the file (a card read from a file, normally); padded to 80 characters, or cut to 80 when longer. A character above 0xFF has no byte and is written as <c>?</c>.</param>
    public void SetCard(string key, string rawCard) => SetValueCards(key, [rawCard]);

    /// <summary>
    /// Replaces the cards of a keyword's value (its first card and the CONTINUE cards right under
    /// it, <see cref="ValueCards"/>) with the given ones, where they stand, or appends them when
    /// the header has the keyword nowhere: how a copy sets a source's value, verbatim, a long
    /// string whole (R-010). A second card of the keyword further down is left as it is.
    /// </summary>
    /// <param name="key">The keyword the cards are filed under, case-sensitive.</param>
    /// <param name="rawCards">The cards, the keyword's own first, then its CONTINUE cards, each one byte per character; each padded to 80 or cut at 80; at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">No card is given.</exception>
    public void SetValueCards(string key, IReadOnlyList<string> rawCards)
    {
        ArgumentNullException.ThrowIfNull(rawCards);
        ArgumentOutOfRangeException.ThrowIfZero(rawCards.Count, nameof(rawCards));

        var cards = rawCards.Select(Pad).ToList();
        var i = IndexOf(key);
        if (i >= 0)
        {
            _cards.RemoveRange(i, 1 + ContinuationsUnder(i));
            _cards.InsertRange(i, cards);
        }
        else
        {
            _cards.AddRange(cards);
        }
    }

    /// <summary>
    /// Removes every card of a keyword, wherever it sits, each with the CONTINUE cards right under
    /// it, the other cards keeping their order: how a copy drops the destination's old
    /// plate-solve cards (R-016) and its checksum (R-018).
    /// </summary>
    /// <param name="key">The keyword, case-sensitive, as <see cref="Contains"/> takes it.</param>
    /// <returns>The cards removed, raw, in the order the header held them, the CONTINUE ones included; empty when no card had the keyword.</returns>
    public IReadOnlyList<string> RemoveCards(string key)
    {
        var removed = new List<string>();
        var i = 0;
        while (i < _cards.Count)
        {
            if (string.Equals(KeywordOf(_cards[i]), key, StringComparison.Ordinal))
            {
                var n = 1 + ContinuationsUnder(i);
                removed.AddRange(_cards.GetRange(i, n));
                _cards.RemoveRange(i, n);
            }
            else
            {
                i++;
            }
        }

        return removed;
    }

    /// <summary>Every card of a keyword, each with the CONTINUE cards right under it, raw, in the order the header holds them; a header may hold a keyword more than once.</summary>
    /// <param name="key">The keyword, case-sensitive.</param>
    /// <returns>The cards, the CONTINUE ones included; empty when no card has the keyword.</returns>
    public IReadOnlyList<string> CardsOf(string key)
    {
        var cards = new List<string>();
        var i = 0;
        while (i < _cards.Count)
        {
            if (string.Equals(KeywordOf(_cards[i]), key, StringComparison.Ordinal))
            {
                var n = 1 + ContinuationsUnder(i);
                cards.AddRange(_cards.GetRange(i, n));
                i += n;
            }
            else
            {
                i++;
            }
        }

        return cards;
    }

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
    /// Writes this header, END and the padding to the next 2880-byte boundary, followed by the
    /// data area copied from <paramref name="dataSource"/>, into a temporary file beside the
    /// target, flushed to the disk and put in the target's place at the end, an existing target
    /// replaced so that it keeps its creation time, attributes and permissions (R-012, ADR 0031):
    /// a write that fails at any point leaves the target as it was, or absent, and its temporary
    /// file is deleted (R-014). The temporary
    /// file has a name of its own (<see cref="TemporaryBeside"/>) and is created only when no
    /// file holds that name, so no file of the user's is overwritten; an in-place write goes the
    /// same way, since the header may have grown past its original blocks and cannot be patched
    /// in place. Both paths are made whole once, as the write begins, so a working folder that
    /// moves while the data streams (a file dialog of the window) changes neither the data read
    /// nor the file replaced (R-012). An exception goes on as the system raised it, holding the
    /// temporary file's path and the target's, so that <see cref="WriteErrorMessage"/> names the
    /// target where its message names the temporary file, whose name the user never typed (R-014).
    /// Each card's characters are written as the bytes they were read from
    /// (<see cref="CardEncoding"/>). The diagnostic log gets the write before it starts, with its
    /// temporary file (at Trace every card to be written, so a failed write still shows the
    /// header it meant to write), the file written after it, and the removal of a failed write's
    /// temporary file (R-042, R-044); a write that throws is its caller's to log.
    /// </summary>
    /// <param name="dataSource">The FITS file whose data area, from its own header's end, is copied; normally the file this header was read from. A relative path is taken from the working folder as the write begins.</param>
    /// <param name="target">The file to write: a new file, an existing one, replaced only at the end, or the same path as <paramref name="dataSource"/> for an in-place rewrite. A relative path is taken from the working folder as the write begins.</param>
    /// <exception cref="IOException">The target or its temporary file cannot be created, written or renamed (a missing folder, a full disk, a data source that cannot be read); its message is shown through <see cref="WriteErrorMessage"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">The target cannot be replaced by this user (read-only, a folder, no access); its message is shown the same way.</exception>
    /// <exception cref="ArgumentException">A path is empty or blank.</exception>
    public void WriteWithDataFrom(string dataSource, string target)
    {
        string data = Path.GetFullPath(dataSource);
        string full = Path.GetFullPath(target);
        string temp = TemporaryBeside(full);

        Log.Debug("write " + target + " through " + temp + ", the data from byte " + LogText.Int(_dataOffset)
                  + " of " + dataSource);
        TraceCards(target);

        long headerBytes;
        long total;
        bool created = false;
        bool moved = false;
        bool kept = false;
        string? backup = null;
        try
        {
            // The input first: a data source that cannot be read leaves no temporary file.
            using (var input = File.OpenRead(data))
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
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

                // On the disk before the rename: the file system journals the rename, not the
                // data, so a power cut right after an unflushed copy could leave the target's
                // name on a file not yet written (ADR 0031).
                output.Flush(flushToDisk: true);
            }

            // An existing file is replaced by Windows' replace, which gives the new file the old
            // one's creation time, attributes and permissions, as a move does not; a permission
            // the drive cannot carry over (FAT, some shares) does not fail the copy. The old file
            // goes to a backup beside it, deleted after the replace, so a replace that fails
            // halfway leaves it under a name the write knows and can put back (ADR 0031).
            if (Directory.Exists(full))
            {
                // A folder of that name: refused as the move over it always was, access denied.
                File.Move(temp, full, overwrite: true);
            }
            else if (File.Exists(full))
            {
                backup = BackupBeside(full);
                (ReplaceForTests ?? ((t, f, b) => File.Replace(t, f, b, ignoreMetadataErrors: true)))(temp, full, backup);
                moved = true;
                DeleteBackup(backup);
            }
            else
            {
                File.Move(temp, full);
            }

            moved = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The user typed the target, not its temporary file: the message they are shown names
            // the target (WriteErrorMessage, R-014); the exception itself goes on as the system
            // raised it, and the log writes it so.
            ex.Data[TemporaryKey] = temp;
            ex.Data[TargetKey] = full;
            if (backup is not null && !moved && !PutBack(backup, full, temp))
            {
                kept = true;
                ex.Data[KeptKey] = backup;
            }

            throw;
        }
        finally
        {
            // A kept backup means the old file is not where it was: nothing more is deleted.
            if (created && !moved && !kept)
            {
                DeleteTemporary(temp);
            }
        }

        Log.Info("wrote " + target + " (" + LogText.Size(total) + ") bytes=" + LogText.Int(total)
                 + " cards=" + LogText.Int(_cards.Count) + " blocks=" + LogText.Int(headerBytes / BlockSize)
                 + " data=" + LogText.Int(total - headerBytes));
    }

    /// <summary>
    /// The path of a write's temporary file: in the target's folder, so the rename at the end
    /// stays on one volume, named as the target followed by <c>.copywcs-</c>, 8 random
    /// hexadecimal digits and <c>.tmp</c> (<c>stack.fit.copywcs-3f9a0c1e.tmp</c>), a name that
    /// tells which program left it should a killed process leave one. A target name too long to
    /// take the 21-character suffix within <see cref="NameLimit"/> is shortened in the temporary
    /// name, so every name the file system accepts can be written (R-012).
    /// </summary>
    /// <param name="full">The full path of the file to write.</param>
    /// <param name="extension">The name's last four characters: <c>.tmp</c> for the temporary file, <c>.bak</c> for the backup of a replaced target.</param>
    /// <returns>The temporary file's full path; the write creates it only when no file holds it.</returns>
    private static string TemporaryBeside(string full, string extension = ".tmp")
    {
        string folder = Path.GetDirectoryName(full) ?? full;
        string name = Path.GetFileName(full);
        string suffix = ".copywcs-" + Guid.NewGuid().ToString("N")[..8] + extension;
        if (name.Length + suffix.Length > NameLimit)
        {
            int keep = NameLimit - suffix.Length;

            // A character outside the Basic Multilingual Plane is two UTF-16 units: keep both or neither.
            if (char.IsHighSurrogate(name[keep - 1]))
            {
                keep--;
            }

            name = name[..keep];
        }

        return Path.Combine(folder, name + suffix);
    }

    /// <summary>
    /// The name of the backup Windows' replace moves the old target to (ADR 0031): as the
    /// temporary file's, ending in <c>.bak</c>, <c>stack.fit.copywcs-3f9a0c1e.bak</c>, and one no
    /// file holds, since the replace would overwrite it.
    /// </summary>
    /// <param name="full">The full path of the target being replaced.</param>
    /// <returns>The backup's full path, beside the target.</returns>
    private static string BackupBeside(string full)
    {
        string backup;
        do
        {
            backup = TemporaryBeside(full, ".bak");
        }
        while (Path.Exists(backup));

        return backup;
    }

    /// <summary>
    /// Deletes the backup of the old target after a replace that succeeded (ADR 0031); a delete
    /// that fails leaves the old file under the backup's name and is a Warn line, the copy itself
    /// being done.
    /// </summary>
    /// <param name="backup">The backup, which the replace created.</param>
    private static void DeleteBackup(string backup)
    {
        try
        {
            File.Delete(backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(ex, "the backup " + backup + " of the replaced file could not be removed");
        }
    }

    /// <summary>
    /// After a replace that failed, puts the old target back from its backup when Windows left it
    /// there and the target's name is free (ERROR_UNABLE_TO_MOVE_REPLACEMENT_2, 1177), so the
    /// folder is as it was (R-014, ADR 0031); a failure that left the old file in place made no
    /// backup, and there is nothing to do.
    /// </summary>
    /// <param name="backup">The backup's full path.</param>
    /// <param name="full">The target's full path.</param>
    /// <param name="temp">The write's temporary file, named in the log when the backup is kept.</param>
    /// <returns>True when no backup is left; false when it is kept.</returns>
    private static bool PutBack(string backup, string full, string temp)
    {
        if (!File.Exists(backup))
        {
            return true;
        }

        if (!Path.Exists(full))
        {
            try
            {
                File.Move(backup, full);
                Log.Warn("the replace of " + full + " failed halfway; the old file was put back from " + backup);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(ex, "the old file could not be put back from " + backup + " to " + full);
            }
        }

        Log.Error("the replace of " + full + " failed halfway; the old file is kept as " + backup
                  + " and the new one as " + temp);
        return false;
    }

    /// <summary>
    /// Deletes the temporary file of a write that failed (R-014), and says so in the log; a
    /// delete that fails is a Warn line, never a second exception over the write's own.
    /// </summary>
    /// <param name="temp">The temporary file, which this write created.</param>
    private static void DeleteTemporary(string temp)
    {
        try
        {
            File.Delete(temp);
            Log.Debug("removed " + temp + ", the temporary file of the failed write");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(ex, "the temporary file " + temp + " of the failed write could not be removed");
        }
    }

    /// <summary>
    /// The message of an exception, as the user is shown it after a failed write: where the
    /// system's message names the write's temporary file, it names the target instead
    /// (<c>Could not find a part of the path 'E:\out\final_wcs.fit'.</c>), the file the user asked
    /// for (R-014). A rename the system refused names no path at all, and stays so. A write that
    /// could not put back the old target adds where it is kept (ADR 0031).
    /// </summary>
    /// <param name="ex">The exception <see cref="WriteWithDataFrom"/> threw, or any other; not null.</param>
    /// <returns>The message, the temporary file's path replaced by the target's; any other exception's message as it is.</returns>
    internal static string WriteErrorMessage(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        string message = ex.Data[TemporaryKey] is string temp && ex.Data[TargetKey] is string full
            ? ex.Message.Replace(temp, full, StringComparison.Ordinal)
            : ex.Message;
        return ex.Data[KeptKey] is string kept
            ? message + T($" The file was kept as '{kept}'.", $" Fisierul a fost pastrat ca '{kept}'.")
            : message;
    }
}
