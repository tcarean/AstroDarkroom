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

namespace CopyWcs.Tests;

/// <summary>
/// Builds synthetic FITS cards and files for the tests. It uses its own 80/2880
/// constants (not FitsHeader's), so that a wrong constant in production is caught by
/// the tests, not confirmed by them.
/// </summary>
internal static class FitsTestData
{
    private const int CardSize = 80;
    private const int BlockSize = 2880;

    public static string Card(string keyword, string value, string? comment = null)
    {
        var line = $"{keyword,-8}= {value}";
        if (comment != null)
        {
            line += $" / {comment}";
        }

        return Pad(line);
    }

    public static string Pad(string card) =>
        card.Length >= CardSize ? card[..CardSize] : card.PadRight(CardSize);

    /// <summary>The card without padding to 80, to test the padding and the cut explicitly.</summary>
    public static string UnpaddedCard(string keyword, string value) => $"{keyword,-8}= {value}";

    /// <summary>Cards padded to 80 and concatenated, WITHOUT an END card and without padding to 2880.</summary>
    public static byte[] RawBytes(IEnumerable<string> cards)
    {
        using var ms = new MemoryStream();
        foreach (var c in cards)
        {
            ms.Write(Encoding.Latin1.GetBytes(Pad(c)));
        }

        return ms.ToArray();
    }

    public static byte[] HeaderBlock(IEnumerable<string> cards)
    {
        using var ms = new MemoryStream();
        foreach (var c in cards)
        {
            ms.Write(Encoding.Latin1.GetBytes(Pad(c)));
        }

        ms.Write(Encoding.Latin1.GetBytes(Pad("END")));

        var remainder = (int)(ms.Position % BlockSize);
        if (remainder != 0)
        {
            var fill = new byte[BlockSize - remainder];
            Array.Fill(fill, (byte)' ');
            ms.Write(fill);
        }
        return ms.ToArray();
    }

    public static byte[] MinimalFitsFile(IEnumerable<string> cards, byte[]? data = null)
    {
        using var ms = new MemoryStream();
        ms.Write(HeaderBlock(cards));
        if (data != null)
        {
            ms.Write(data);
        }

        return ms.ToArray();
    }

    public static byte[] DeterministicData(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        return data;
    }

    /// <summary>A minimal plate-solved header: standard WCS, one SIP coefficient and optical keys.</summary>
    public static List<string> PlateSolvedCards(int naxis1 = 100, int naxis2 = 80) => new()
    {
        Card("SIMPLE", "T"),
        Card("BITPIX", "16"),
        Card("NAXIS", "2"),
        Card("NAXIS1", naxis1.ToString()),
        Card("NAXIS2", naxis2.ToString()),
        Card("CTYPE1", "'RA---TAN-SIP'", "TAN + SIP"),
        Card("CTYPE2", "'DEC--TAN-SIP'"),
        Card("CRPIX1", "50.0"),
        Card("CRPIX2", "40.0"),
        Card("CRVAL1", "83.633083"),
        Card("CRVAL2", "22.014500"),
        Card("CDELT1", "-0.000305"),
        Card("CDELT2", "0.000305"),
        Card("A_0_2", "1.2e-06"),
        Card("BP_1_1", "-3.4e-07"),
        Card("PLTSOLVD", "T"),
        Card("FOCALLEN", "700.0"),
        Card("XPIXSZ", "3.76"),
        Card("OBJECT", "'M42'"),
        Card("TELESCOP", "'My Scope'"),
    };

    /// <summary>A processed image's header: no WCS, the same dimensions as the default source.</summary>
    public static List<string> ProcessedCards(int naxis1 = 100, int naxis2 = 80) => new()
    {
        Card("SIMPLE", "T"),
        Card("BITPIX", "16"),
        Card("NAXIS", "2"),
        Card("NAXIS1", naxis1.ToString()),
        Card("NAXIS2", naxis2.ToString()),
        Card("OBJECT", "'M42'"),
    };
}
