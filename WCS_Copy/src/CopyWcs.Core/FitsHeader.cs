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
using static CopyWcs.Ui;

namespace CopyWcs;

/// <summary>
/// The header of the primary HDU, as a list of raw 80-character cards, read and written by
/// hand: no FITS library, no package (ADR 0001). How the cards, the blocks and the in-place
/// write fit together is in <c>docs/design/fits-header.md</c>.
/// </summary>
internal sealed class FitsHeader
{
    private const int CardSize = 80;
    private const int BlockSize = 2880;

    private readonly List<string> _cards = new();

    /// <summary>The offset in the file where the data area begins, after the padded header.</summary>
    private long _dataOffset;

    /// <summary>The keyword of every card that has one, in the header's order; blank cards are left out.</summary>
    public IEnumerable<string> Keywords => _cards.Select(KeywordOf).Where(k => k.Length > 0);

    /// <summary>Reads the primary header of a FITS file.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The header, with the data offset remembered for <see cref="WriteWithDataFrom"/>.</returns>
    /// <exception cref="InvalidDataException">The file does not begin with SIMPLE, or ends before an END card.</exception>
    /// <exception cref="IOException">The file cannot be opened or read.</exception>
    public static FitsHeader ReadPrimary(string path)
    {
        using var fs = File.OpenRead(path);
        return ReadFrom(fs);
    }

    /// <summary>
    /// Reads a primary header from the start of a stream: whole 2880-byte blocks, card by
    /// card, up to and including the block that holds END.
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
                var card = Encoding.ASCII.GetString(block, i, CardSize);
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

    /// <summary>The keyword of a card: its first eight characters, trimmed.</summary>
    /// <param name="card">The card; shorter than eight characters means no keyword.</param>
    /// <returns>The keyword, or an empty string for a blank or short card.</returns>
    private static string KeywordOf(string card) =>
        card.Length < 8 ? string.Empty : card[..8].Trim();

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

    /// <summary>The integer value of a card: the text between the '=' and the '/' comment, if any.</summary>
    /// <param name="key">The keyword, case-sensitive; safe for NAXIS1 and the other integer keys.</param>
    /// <returns>The value, or -1 when the card is missing, has no '=' or does not hold an integer.</returns>
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

        return int.TryParse(value.Trim(), out var n) ? n : -1;
    }

    /// <summary>Replaces the card of a keyword in place, or appends it when the header has none.</summary>
    /// <param name="key">The keyword the card is filed under, case-sensitive.</param>
    /// <param name="rawCard">The card's text; padded to 80 characters, or cut to 80 when longer.</param>
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

    /// <summary>Appends a HISTORY card.</summary>
    /// <param name="text">The text after the keyword; cut to 72 characters, the room a card has after "HISTORY ".</param>
    public void AddHistory(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = text.Length > 72 ? text[..72] : text;
        _cards.Add(Pad("HISTORY " + body));
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
    /// header may have grown past its original blocks, so it cannot be patched in place.
    /// </summary>
    /// <param name="dataSource">The FITS file whose data area, from its own header's end, is copied; normally the file this header was read from.</param>
    /// <param name="target">The file to write; the same path as <paramref name="dataSource"/> for an in-place rewrite.</param>
    /// <exception cref="IOException">The target cannot be created or written.</exception>
    public void WriteWithDataFrom(string dataSource, string target)
    {
        var inPlace = Path.GetFullPath(dataSource) == Path.GetFullPath(target);
        var temp = inPlace ? target + ".tmp" : target;

        using (var input = File.OpenRead(dataSource))
        using (var output = File.Create(temp))
        {
            foreach (var card in _cards)
            {
                output.Write(Encoding.ASCII.GetBytes(Pad(card)));
            }

            output.Write(Encoding.ASCII.GetBytes("END".PadRight(CardSize)));

            // Spaces up to the next 2880-byte boundary.
            var remainder = (int)(output.Position % BlockSize);
            if (remainder != 0)
            {
                var fill = new byte[BlockSize - remainder];
                Array.Fill(fill, (byte)' ');
                output.Write(fill);
            }

            input.Seek(_dataOffset, SeekOrigin.Begin);
            input.CopyTo(output);
        }

        if (inPlace)
        {
            File.Move(temp, target, overwrite: true);
        }
    }
}
