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

namespace ImageVisualCompare.Core;

/// <summary>
/// Where the image of a FITS file is and how its samples are stored: everything the header
/// says that the decoder needs, and nothing else of it.
/// </summary>
/// <param name="Planes">1, shown gray, or 3: red, green, blue, one whole plane after another.</param>
/// <param name="BitPix">The FITS sample type: 8, 16, 32 for integers, -32, -64 for floats.</param>
/// <param name="BottomUp">The first row in the file is the bottom row of the image.</param>
/// <param name="DataOffset">Where the first sample is, in bytes from the start of the file.</param>
public sealed record FitsImageLayout(
    int Width,
    int Height,
    int Planes,
    int BitPix,
    double BZero,
    double BScale,
    bool BottomUp,
    long DataOffset)
{
    public bool IsFloat => BitPix < 0;

    public int BitsPerChannel => Math.Abs(BitPix);

    public int BytesPerSample => BitsPerChannel / 8;

    public long PlaneBytes => (long)Width * Height * BytesPerSample;

    public long DataBytes => PlaneBytes * Planes;

    /// <summary>The image row, 0 at the top, that a row of the file is.</summary>
    public int DisplayRow(int fileRow) => BottomUp ? Height - 1 - fileRow : fileRow;

    /// <param name="valueRange">
    /// Set only once the samples have been seen, for a float file that was not in 0 to 1.
    /// </param>
    public ImageInfo Describe(string path, string? valueRange = null) => new(
        path,
        Width,
        Height,
        BitsPerChannel,
        Planes == 3 ? ColorChannels.Rgb : ColorChannels.Gray,
        IsFloat,
        valueRange);
}

/// <summary>
/// Reads the header of a FITS file: 80-byte cards in blocks of 2880 bytes, one header and one
/// data block per unit, the units one after another. Started from the reader of
/// AstroPhotoAnalyzer; what differs is in ADR 0029.
/// </summary>
public static class FitsHeader
{
    public const int BlockSize = 2880;

    private const int CardSize = 80;

    /// <summary>The most axes the standard allows a unit.</summary>
    private const int MaxAxes = 999;

    /// <summary>The widest row whose samples, at 8 bytes each, still fit one array.</summary>
    private const int MaxWidth = int.MaxValue / 8;

    private static readonly byte[] Signature = "SIMPLE  ="u8.ToArray();

    /// <summary>
    /// Whether the stream starts as every FITS file does. The extension is not asked: a FITS
    /// picked under "All files" is still one, and a PNG named .fit is still not.
    /// </summary>
    public static bool IsFits(Stream stream)
    {
        Span<byte> start = stackalloc byte[Signature.Length];
        return stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) == start.Length
            && start.SequenceEqual(Signature);
    }

    /// <summary>
    /// The first unit that holds an image: the primary, or an IMAGE extension. An empty primary
    /// and a table are stepped over. Reads headers only, and checks against the length of the
    /// stream that the data is all there. The header is not trusted: a size that is negative,
    /// or that no file could hold, is refused before anything is computed from it.
    /// </summary>
    /// <exception cref="NotSupportedException">The file has no image this program can show.</exception>
    /// <exception cref="EndOfStreamException">The file is cut short.</exception>
    public static FitsImageLayout ReadLayout(Stream stream)
    {
        bool primary = true;
        while (true)
        {
            Dictionary<string, string>? cards = ReadCards(stream, primary);
            if (cards is null)
            {
                throw NoImage();
            }

            if (IsTrue(cards, "ZIMAGE"))
            {
                throw new NotSupportedException("FITS: a tile-compressed image is not read.");
            }

            long axes = Count(cards, "NAXIS", 0);
            if (axes > MaxAxes)
            {
                throw InvalidHeader();
            }

            long width = Count(cards, "NAXIS1", 0);
            long height = Count(cards, "NAXIS2", 0);
            bool holdsImages = primary || Text(cards, "XTENSION") == "IMAGE";

            if (holdsImages && axes >= 2 && width > 0 && height > 0)
            {
                return Layout(cards, (int)axes, width, height, stream);
            }

            long next = stream.Position + DataBlockBytes(cards, (int)axes);
            if (next > stream.Length || next < 0)
            {
                throw NoImage();
            }

            stream.Position = next;
            primary = false;
        }
    }

    /// <summary>
    /// The bytes a unit's data takes in the file, to the end of its last block: the standard's
    /// |BITPIX| / 8 x GCOUNT x (PCOUNT + NAXIS1 x ... x NAXISn). Random groups, whose first
    /// axis is 0, leave that axis out of the product.
    /// </summary>
    private static long DataBlockBytes(Dictionary<string, string> cards, int axes)
    {
        try
        {
            checked
            {
                // A unit with no axes has no data at all, not one sample: the empty product
                // would be 1, and the next header would be looked for 2880 bytes too far.
                long samples = axes == 0 ? 0 : 1;
                bool groups = IsTrue(cards, "GROUPS") && Count(cards, "NAXIS1", 1) == 0;
                for (int axis = groups ? 2 : 1; axis <= axes; axis++)
                {
                    samples *= Count(cards, "NAXIS" + axis.ToString(CultureInfo.InvariantCulture), 1);
                }

                long bytesPerSample = Math.Abs(Count(cards, "BITPIX", 0, signed: true)) / 8;
                long dataBytes = bytesPerSample * Count(cards, "GCOUNT", 1) * (Count(cards, "PCOUNT", 0) + samples);
                return (dataBytes + BlockSize - 1) / BlockSize * BlockSize;
            }
        }
        catch (OverflowException)
        {
            throw InvalidHeader();
        }
    }

    private static FitsImageLayout Layout(
        Dictionary<string, string> cards, int axes, long width, long height, Stream stream)
    {
        if (axes > 3)
        {
            throw new NotSupportedException(
                $"FITS: an image with {axes} axes is not read; 2, or 3 with 1 or 3 planes, are.");
        }

        long planes = axes == 3 ? Count(cards, "NAXIS3", 1) : 1;
        if (planes is not (1 or 3))
        {
            throw new NotSupportedException($"FITS: an image with {planes} planes is not read; 1 or 3 are.");
        }

        long bitPix = Count(cards, "BITPIX", 0, signed: true);
        if (bitPix is not (8 or 16 or 32 or -32 or -64))
        {
            throw new NotSupportedException($"FITS: BITPIX {bitPix} is not read.");
        }

        // A row of samples and a row of Bgra32 are each one array, so a width past this cannot
        // be decoded whatever the machine; the height only has to be a row number.
        if (width > MaxWidth || height > int.MaxValue)
        {
            throw TooLarge(width, height);
        }

        // Siril writes its rows bottom first and says so. A file that says nothing is bottom
        // first too: that is the FITS convention, and how Siril shows such a file (ADR 0029).
        bool bottomUp = !string.Equals(Text(cards, "ROWORDER"), "TOP-DOWN", StringComparison.OrdinalIgnoreCase);

        var layout = new FitsImageLayout(
            (int)width,
            (int)height,
            (int)planes,
            (int)bitPix,
            Number(cards, "BZERO", 0),
            Number(cards, "BSCALE", 1),
            bottomUp,
            stream.Position);

        // 2^31 x 2^31 doubles would not fit a 64-bit count, and a count that wrapped to a
        // negative number would pass the comparison below.
        if (width * height > long.MaxValue / (layout.BytesPerSample * planes) - layout.DataOffset)
        {
            throw TooLarge(width, height);
        }

        if (layout.DataOffset + layout.DataBytes > stream.Length)
        {
            throw new EndOfStreamException("FITS: the file ends before the image data does.");
        }

        return layout;
    }

    private static NotSupportedException NoImage() => new("FITS: no image found in the file.");

    private static NotSupportedException InvalidHeader() => new("FITS: the header is not valid.");

    private static NotSupportedException TooLarge(long width, long height) =>
        new($"FITS: an image of {width} × {height} is too large to show.");

    /// <summary>
    /// The cards of one header up to END, or null when no header starts here: at the end of the
    /// stream, or, after the primary, at bytes that are not an extension (some programs leave
    /// padding or their own data after the last unit).
    /// </summary>
    private static Dictionary<string, string>? ReadCards(Stream stream, bool primary)
    {
        var cards = new Dictionary<string, string>(StringComparer.Ordinal);
        byte[] block = new byte[BlockSize];
        bool first = true;

        while (true)
        {
            int read = stream.ReadAtLeast(block, BlockSize, throwOnEndOfStream: false);
            if (first && !primary && (read < BlockSize || !block.AsSpan().StartsWith("XTENSION"u8)))
            {
                return null;
            }

            if (read == 0 && first)
            {
                return null;
            }

            if (read < BlockSize)
            {
                throw new EndOfStreamException("FITS: the header ends before its END card.");
            }

            first = false;
            for (int offset = 0; offset < BlockSize; offset += CardSize)
            {
                string card = Encoding.ASCII.GetString(block, offset, CardSize);
                string key = card[..8].TrimEnd();
                if (key == "END")
                {
                    return cards;
                }

                if (key.Length > 0 && card[8] == '=')
                {
                    cards[key] = Value(card[10..]);
                }
            }
        }
    }

    /// <summary>
    /// The value of a card without its comment. A string comes without its quotes and without
    /// the spaces FITS pads it with; a slash inside the quotes is not a comment.
    /// </summary>
    private static string Value(string field)
    {
        string trimmed = field.TrimStart();
        if (trimmed.StartsWith('\''))
        {
            int close = trimmed.IndexOf('\'', 1);
            return (close > 0 ? trimmed[1..close] : trimmed[1..]).TrimEnd();
        }

        int slash = field.IndexOf('/', StringComparison.Ordinal);
        return (slash >= 0 ? field[..slash] : field).Trim();
    }

    private static string Text(Dictionary<string, string> cards, string key) =>
        cards.TryGetValue(key, out string? value) ? value : string.Empty;

    private static bool IsTrue(Dictionary<string, string> cards, string key) => Text(cards, key) == "T";

    /// <summary>
    /// A keyword the layout is computed from: an integer, and except for BITPIX not a negative
    /// one. A value that is neither makes the header invalid; it is never read as its default,
    /// which would step over a heap of 3 000 000 000 bytes as if it were empty.
    /// </summary>
    private static long Count(Dictionary<string, string> cards, string key, long fallback, bool signed = false)
    {
        if (!cards.TryGetValue(key, out string? value))
        {
            return fallback;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            || (parsed < 0 && !signed))
        {
            throw InvalidHeader();
        }

        return parsed;
    }

    // FITS allows a Fortran exponent, 1.0D3.
    private static double Number(Dictionary<string, string> cards, string key, double fallback) =>
        cards.TryGetValue(key, out string? value)
        && double.TryParse(value.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;
}
