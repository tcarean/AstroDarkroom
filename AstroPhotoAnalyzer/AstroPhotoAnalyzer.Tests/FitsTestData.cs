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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Construieste fisiere FITS sintetice in memorie. Nimic de aici nu atinge discul, si
/// niciun cadru real nu e in repo - fiecare scena vine dintr-o formula, deci raspunsul
/// asteptat e cunoscut analitic. Pornit din FitsTestData al FlatCheck.
/// </summary>
internal static class FitsTestData
{
    internal const int BlockSize = 2880;
    internal const int CardSize = 80;

    /// <summary>Card in format fix: KEYWORD (8) + "= " + valoarea aliniata la dreapta pe 20, umplut la 80.</summary>
    internal static string Card(string key, string value) =>
        (key.PadRight(8) + "= " + value.PadLeft(20)).PadRight(CardSize);

    /// <summary>Card cu sir: KEYWORD = 'text' [/ comentariu], umplut la 80.</summary>
    internal static string StringCard(string key, string text, string? comment = null) =>
        (key.PadRight(8) + "= '" + text + "'" + (comment == null ? "" : " / " + comment)).PadRight(CardSize);

    static byte[] HeaderBlocks(IEnumerable<string> cards)
    {
        var list = cards.ToList();
        list.Add("END".PadRight(CardSize));
        while (list.Count % 36 != 0) list.Add(new string(' ', CardSize));
        return Encoding.ASCII.GetBytes(string.Concat(list));
    }

    static double DefaultBzero(int bitpix) => bitpix == 16 ? 32768 : bitpix == 32 ? 2147483648 : 0;

    static void AppendSample(List<byte> into, double physical, int bitpix, double bzero, double bscale)
    {
        double stored = (physical - bzero) / bscale;
        switch (bitpix)
        {
            case 8:
                into.Add((byte)Math.Clamp(Math.Round(stored), 0, 255));
                break;
            case 16:
                {
                    short s = (short)Math.Clamp(Math.Round(stored), short.MinValue, short.MaxValue);
                    into.Add((byte)((s >> 8) & 0xFF));
                    into.Add((byte)(s & 0xFF));
                    break;
                }
            case 32:
                {
                    int v = (int)Math.Clamp(Math.Round(stored), int.MinValue, int.MaxValue);
                    for (int k = 3; k >= 0; k--) into.Add((byte)((v >> (k * 8)) & 0xFF));
                    break;
                }
            case -32:
                {
                    int bits = BitConverter.SingleToInt32Bits((float)stored);
                    for (int k = 3; k >= 0; k--) into.Add((byte)((bits >> (k * 8)) & 0xFF));
                    break;
                }
            case -64:
                {
                    long bits = BitConverter.DoubleToInt64Bits(stored);
                    for (int k = 7; k >= 0; k--) into.Add((byte)((bits >> (k * 8)) & 0xFF));
                    break;
                }
            default:
                throw new ArgumentException("BITPIX neacceptat de generator: " + bitpix);
        }
    }

    static byte[] PadToBlock(List<byte> data)
    {
        int pad = (BlockSize - (data.Count % BlockSize)) % BlockSize;
        data.AddRange(new byte[pad]);
        return data.ToArray();
    }

    /// <summary>
    /// Un HDU cu imagine 2D sau cub. <paramref name="pixel"/>(x, y, plan) da valoarea
    /// FIZICA (dupa BZERO/BSCALE): pentru BITPIX 16 e 0..65535, pentru -32 e valoarea reala.
    /// Randul 0 e primul din fisier - cine vrea BOTTOM-UP pune cardul si stie ce face.
    /// </summary>
    internal static byte[] Build(
        int width, int height, int planes, Func<int, int, int, double> pixel,
        int bitpix = 16, double? bzero = null, double bscale = 1,
        IEnumerable<string>? extraCards = null)
    {
        double bz = bzero ?? DefaultBzero(bitpix);
        var cards = new List<string>
        {
            Card("SIMPLE", "T"), Card("BITPIX", bitpix.ToString()),
            Card("NAXIS", planes > 1 ? "3" : "2"),
            Card("NAXIS1", width.ToString()), Card("NAXIS2", height.ToString()),
        };
        if (planes > 1) cards.Add(Card("NAXIS3", planes.ToString()));
        if (bz != 0) cards.Add(Card("BZERO", bz.ToString("0.###")));
        if (bscale != 1) cards.Add(Card("BSCALE", bscale.ToString("0.###")));
        if (extraCards != null) cards.AddRange(extraCards);

        var data = new List<byte>();
        for (int p = 0; p < planes; p++)
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    AppendSample(data, pixel(x, y, p), bitpix, bz, bscale);

        return HeaderBlocks(cards).Concat(PadToBlock(data)).ToArray();
    }

    /// <summary>Imagine 2D, comoditate peste <see cref="Build"/>.</summary>
    internal static byte[] Build2D(int width, int height, Func<int, int, double> pixel,
        int bitpix = 16, double? bzero = null, double bscale = 1, IEnumerable<string>? extraCards = null)
        => Build(width, height, 1, (x, y, _) => pixel(x, y), bitpix, bzero, bscale, extraCards);

    /// <summary>Un HDU primar GOL (NAXIS = 0) urmat de un HDU cu imagine - ca la fisierele cu extensii.</summary>
    internal static byte[] WithEmptyPrimary(byte[] imageHdu)
    {
        var primary = HeaderBlocks(new[] { Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "0"), Card("EXTEND", "T") });
        return primary.Concat(imageHdu).ToArray();
    }
}
