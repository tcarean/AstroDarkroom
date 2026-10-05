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
    /// stream that the data is all there.
    /// </summary>
    /// <exception cref="NotSupportedException">The file has no image this program can show.</exception>
    /// <exception cref="EndOfStreamException">The file is cut short.</exception>
    public static FitsImageLayout ReadLayout(Stream stream)
    {
        bool primary = true;
        while (true)
        {
            Dictionary<string, string>? cards = ReadCards(stream);
            if (cards is null)
            {
                throw new NotSupportedException("FITS: no image found in the file.");
            }

            if (IsTrue(cards, "ZIMAGE"))
            {
                throw new NotSupportedException("FITS: a tile-compressed image is not read.");
            }

            int bitPix = Int(cards, "BITPIX", 0);
            int axes = Int(cards, "NAXIS", 0);
            int width = Int(cards, "NAXIS1", 0);
            int height = Int(cards, "NAXIS2", 0);
            bool holdsImages = primary || Text(cards, "XTENSION") == "IMAGE";

            if (holdsImages && axes >= 2 && width > 0 && height > 0)
            {
                return Layout(cards, bitPix, axes, width, height, stream);
            }

            // A unit with no axes has no data at all, not one sample: the empty product would
            // be 1, and the next header would be looked for 2880 bytes too far.
            long samples = axes == 0 ? 0 : 1;
            for (int axis = 1; axis <= axes; axis++)
            {
                samples *= Int(cards, "NAXIS" + axis.ToString(CultureInfo.InvariantCulture), 1);
            }

            long dataBytes = Math.Abs(bitPix) / 8 * (long)Int(cards, "GCOUNT", 1) * (Int(cards, "PCOUNT", 0) + samples);
            long next = stream.Position + ((dataBytes + BlockSize - 1) / BlockSize * BlockSize);
            if (next > stream.Length)
            {
                throw new NotSupportedException("FITS: no image found in the file.");
            }

            stream.Position = next;
            primary = false;
        }
    }

    private static FitsImageLayout Layout(
        Dictionary<string, string> cards, int bitPix, int axes, int width, int height, Stream stream)
    {
        if (axes > 3)
        {
            throw new NotSupportedException(
                $"FITS: an image with {axes} axes is not read; 2, or 3 with 1 or 3 planes, are.");
        }

        int planes = axes == 3 ? Int(cards, "NAXIS3", 1) : 1;
        if (planes is not (1 or 3))
        {
            throw new NotSupportedException($"FITS: an image with {planes} planes is not read; 1 or 3 are.");
        }

        if (bitPix is not (8 or 16 or 32 or -32 or -64))
        {
            throw new NotSupportedException($"FITS: BITPIX {bitPix} is not read.");
        }

        // Siril writes its rows bottom first and says so. A file that says nothing is bottom
        // first too: that is the FITS convention, and how Siril shows such a file (ADR 0029).
        bool bottomUp = !string.Equals(Text(cards, "ROWORDER"), "TOP-DOWN", StringComparison.OrdinalIgnoreCase);

        var layout = new FitsImageLayout(
            width,
            height,
            planes,
            bitPix,
            Number(cards, "BZERO", 0),
            Number(cards, "BSCALE", 1),
            bottomUp,
            stream.Position);

        if (layout.DataOffset + layout.DataBytes > stream.Length)
        {
            throw new EndOfStreamException("FITS: the file ends before the image data does.");
        }

        return layout;
    }

    /// <summary>The cards of one header up to END, or null when the stream ends where a header would start.</summary>
    private static Dictionary<string, string>? ReadCards(Stream stream)
    {
        var cards = new Dictionary<string, string>(StringComparer.Ordinal);
        byte[] block = new byte[BlockSize];
        bool any = false;

        while (true)
        {
            int read = stream.ReadAtLeast(block, BlockSize, throwOnEndOfStream: false);
            if (read == 0 && !any)
            {
                return null;
            }

            if (read < BlockSize)
            {
                throw new EndOfStreamException("FITS: the header ends before its END card.");
            }

            any = true;
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

    private static int Int(Dictionary<string, string> cards, string key, int fallback) =>
        cards.TryGetValue(key, out string? value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    // FITS allows a Fortran exponent, 1.0D3.
    private static double Number(Dictionary<string, string> cards, string key, double fallback) =>
        cards.TryGetValue(key, out string? value)
        && double.TryParse(value.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;
}
