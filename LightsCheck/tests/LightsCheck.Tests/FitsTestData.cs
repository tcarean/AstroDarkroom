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

namespace LightsCheck.Tests;

/// <summary>
/// Builds synthetic FITS files in memory. Nothing here touches the filesystem, and no
/// real frames are committed to the repo - every scene is generated from a formula so
/// the expected answer is known analytically.
/// </summary>
internal static class FitsTestData
{
    internal const int BlockSize = 2880;
    internal const int CardSize = 80;

    /// <summary>Fixed-format card: KEYWORD (8) + "= " + value right-aligned in 20, padded to 80.</summary>
    internal static string Card(string key, string value) =>
        (key.PadRight(8) + "= " + value.PadLeft(20)).PadRight(CardSize);

    /// <summary>
    /// The optics cards the --light guard reads. With the pixel size the field can be
    /// computed exactly; without it only the focal length remains, and then the guard falls
    /// back on the conservative full-frame assumption - exactly the case of a DSLR passed
    /// through Siril, which does not write XPIXSZ.
    /// </summary>
    internal static string[] OpticsCards(double focalMm, double? pixelUm = null, int binning = 1)
    {
        var cards = new List<string> { Card("FOCALLEN", focalMm.ToString("0.###")) };
        if (pixelUm.HasValue)
        {
            cards.Add(Card("XPIXSZ", pixelUm.Value.ToString("0.###")));
            cards.Add(Card("YPIXSZ", pixelUm.Value.ToString("0.###")));
            cards.Add(Card("XBINNING", binning.ToString()));
            cards.Add(Card("YBINNING", binning.ToString()));
        }
        return cards.ToArray();
    }

    /// <summary>The scale measured by a plate solve, in degrees/pixel.</summary>
    internal static string[] WcsCards(double degPerPixel) =>
        new[] { Card("CDELT1", degPerPixel.ToString("0.#########")) };

    /// <summary>
    /// The mosaic order, as Siril writes it on a frame passed through Conversion. The
    /// offsets are optional because Siril too writes them only when there is a crop.
    /// ROWORDER is optional for another reason: Siril always writes it ('BOTTOM-UP'), but
    /// most fixtures here do not want to test it, and without the card the reader moves
    /// nothing - exactly as before it existed.
    /// </summary>
    internal static string[] BayerCards(string pattern, int xOffset = 0, int yOffset = 0, string? rowOrder = null)
    {
        var cards = new List<string> { Card("BAYERPAT", "'" + pattern + "'") };
        if (xOffset != 0)
        {
            cards.Add(Card("XBAYROFF", xOffset.ToString()));
        }

        if (yOffset != 0)
        {
            cards.Add(Card("YBAYROFF", yOffset.ToString()));
        }

        if (rowOrder != null)
        {
            cards.Add(Card("ROWORDER", "'" + rowOrder + "'"));
        }

        return cards.ToArray();
    }

    static byte[] HeaderBlocks(IEnumerable<string> cards)
    {
        var list = cards.ToList();
        list.Add("END".PadRight(CardSize));
        while (list.Count % 36 != 0)
        {
            list.Add(new string(' ', CardSize));
        }

        return Encoding.ASCII.GetBytes(string.Concat(list));
    }

    static double DefaultBzero(int bitpix) => bitpix == 16 ? 32768 : 0;

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
                    for (int k = 3; k >= 0; k--)
                    {
                        into.Add((byte)((v >> (k * 8)) & 0xFF));
                    }

                    break;
                }
            case -32:
                {
                    int bits = BitConverter.SingleToInt32Bits((float)stored);
                    for (int k = 3; k >= 0; k--)
                    {
                        into.Add((byte)((bits >> (k * 8)) & 0xFF));
                    }

                    break;
                }
            case -64:
                {
                    long bits = BitConverter.DoubleToInt64Bits(stored);
                    for (int k = 7; k >= 0; k--)
                    {
                        into.Add((byte)((bits >> (k * 8)) & 0xFF));
                    }

                    break;
                }
            default:
                throw new ArgumentException("BITPIX not supported by the builder: " + bitpix);
        }
    }

    static byte[] PadToBlock(List<byte> data)
    {
        int pad = (BlockSize - (data.Count % BlockSize)) % BlockSize;
        data.AddRange(new byte[pad]);
        return data.ToArray();
    }

    /// <summary>A single-HDU 2-D image.</summary>
    internal static byte[] BuildFits(
        int width, int height, Func<int, int, double> pixel,
        int bitpix = 16, double? bzero = null, double bscale = 1,
        IEnumerable<string>? extraCards = null)
    {
        double bz = bzero ?? DefaultBzero(bitpix);
        var cards = new List<string>
        {
            Card("SIMPLE", "T"), Card("BITPIX", bitpix.ToString()), Card("NAXIS", "2"),
            Card("NAXIS1", width.ToString()), Card("NAXIS2", height.ToString()),
            Card("BZERO", bz.ToString("0.###")), Card("BSCALE", bscale.ToString("0.###")),
        };
        if (extraCards != null)
        {
            cards.AddRange(extraCards);
        }

        var data = new List<byte>(width * height * Math.Abs(bitpix) / 8);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                AppendSample(data, pixel(x, y), bitpix, bz, bscale);
            }
        }

        return HeaderBlocks(cards).Concat(PadToBlock(data)).ToArray();
    }

    /// <summary>A plane-sequential colour cube (Siril writes NAXIS3 planes back to back).</summary>
    internal static byte[] BuildFitsCube(
        int width, int height, int planes, Func<int, int, int, double> pixel,
        int bitpix = 16, double? bzero = null, double bscale = 1)
    {
        double bz = bzero ?? DefaultBzero(bitpix);
        var cards = new List<string>
        {
            Card("SIMPLE", "T"), Card("BITPIX", bitpix.ToString()), Card("NAXIS", "3"),
            Card("NAXIS1", width.ToString()), Card("NAXIS2", height.ToString()),
            Card("NAXIS3", planes.ToString()),
            Card("BZERO", bz.ToString("0.###")), Card("BSCALE", bscale.ToString("0.###")),
        };
        var data = new List<byte>();
        for (int p = 0; p < planes; p++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    AppendSample(data, pixel(x, y, p), bitpix, bz, bscale);
                }
            }
        }

        return HeaderBlocks(cards).Concat(PadToBlock(data)).ToArray();
    }

    /// <summary>Empty NAXIS=0 primary HDU followed by an IMAGE extension holding the data.</summary>
    internal static byte[] BuildFitsWithEmptyPrimary(
        int width, int height, Func<int, int, double> pixel, int bitpix = 16, double? bzero = null)
    {
        double bz = bzero ?? DefaultBzero(bitpix);
        var primary = HeaderBlocks(new[]
        {
            Card("SIMPLE", "T"), Card("BITPIX", "8"), Card("NAXIS", "0"),
        });
        var ext = HeaderBlocks(new[]
        {
            Card("XTENSION", "IMAGE"), Card("BITPIX", bitpix.ToString()), Card("NAXIS", "2"),
            Card("NAXIS1", width.ToString()), Card("NAXIS2", height.ToString()),
            Card("PCOUNT", "0"), Card("GCOUNT", "1"),
            Card("BZERO", bz.ToString("0.###")), Card("BSCALE", "1"),
        });
        var data = new List<byte>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                AppendSample(data, pixel(x, y), bitpix, bz, 1);
            }
        }

        return primary.Concat(ext).Concat(PadToBlock(data)).ToArray();
    }

    /// <summary>A 1-D HDU - no 2-D image anywhere in the file.</summary>
    internal static byte[] BuildFitsNaxis1(int length)
    {
        var hdr = HeaderBlocks(new[]
        {
            Card("SIMPLE", "T"), Card("BITPIX", "16"), Card("NAXIS", "1"),
            Card("NAXIS1", length.ToString()), Card("BZERO", "32768"),
        });
        return hdr.Concat(new byte[BlockSize]).ToArray();
    }

    // ------------------------------------------------------------------ scenes

    /// <summary>
    /// The ideal flat: a purely radial field, centre <paramref name="center"/> ADU, falling
    /// to center*(1-k) at the corner. Every structure metric should read ~0 on this.
    /// </summary>
    internal static Func<int, int, double> Radial(int w, int h, double center = 30000, double k = 0.45)
    {
        double cx = w / 2.0, cy = h / 2.0;
        double rmax = Math.Sqrt(cx * cx + cy * cy);
        return (x, y) =>
        {
            double dx = x - cx, dy = y - cy;
            double rn = Math.Sqrt(dx * dx + dy * dy) / rmax;
            return center * (1.0 - k * rn * rn);
        };
    }

    /// <summary>Multiplies by a horizontal linear ramp of total amplitude <paramref name="g"/>.</summary>
    internal static Func<int, int, double> WithLinearGradient(Func<int, int, double> b, int w, double g) =>
        (x, y) => b(x, y) * (1.0 + g * (x / (double)w - 0.5));

    /// <summary>
    /// Multiplies by a saddle <c>1 + s * xn * yn</c> (xn, yn in [-1, 1]): the even non-radial
    /// term a twilight sky leaves in a wide-field flat, which a 180-degree rotation cannot cancel.
    /// </summary>
    internal static Func<int, int, double> WithSaddle(Func<int, int, double> b, int w, int h, double s) =>
        (x, y) => b(x, y) * (1.0 + s * (2.0 * x / w - 1.0) * (2.0 * y / h - 1.0));

    /// <summary>
    /// Multiplies by a four-fold term <c>1 + a * rn^2 * sin(4 theta + phase)</c> (rn = 1 at the
    /// corner): the even non-radial family a master built from four orientations rotated 90
    /// degrees keeps - the world's blotches smeared four-fold - which has no 2-theta part at
    /// all, so a ring fit with only 2-theta in its basis reads it as 2-theta on the partial
    /// arcs. Amplitude <paramref name="a"/> at the corner, <paramref name="phaseDeg"/> so the
    /// lobes do not sit on the axes.
    /// </summary>
    internal static Func<int, int, double> WithFourfold(Func<int, int, double> b, int w, int h, double a, double phaseDeg = 20)
    {
        double cx = w / 2.0, cy = h / 2.0, rmax = Math.Sqrt(cx * cx + cy * cy), phase = phaseDeg * Math.PI / 180;
        return (x, y) =>
        {
            double dx = x - cx, dy = y - cy;
            double rn2 = (dx * dx + dy * dy) / (rmax * rmax);
            return b(x, y) * (1.0 + a * rn2 * Math.Sin(4 * Math.Atan2(dy, dx) + phase));
        };
    }

    /// <summary>Per-Bayer-position gain, so the four CFA channels separate (RGGB-like).</summary>
    internal static Func<int, int, double> WithBayerMosaic(
        Func<int, int, double> b, double rGain = 0.62, double bGain = 0.48) =>
        (x, y) =>
        {
            double g = 1.0;
            if (y % 2 == 0 && x % 2 == 0)
            {
                g = rGain;
            }
            else if (y % 2 == 1 && x % 2 == 1)
            {
                g = bGain;
            }

            return b(x, y) * g;
        };

    /// <summary>Adds a flat pedestal, as an uncalibrated frame still carrying its offset.</summary>
    internal static Func<int, int, double> WithOffset(Func<int, int, double> b, double offset) =>
        (x, y) => b(x, y) + offset;

    /// <summary>Scales the whole frame, e.g. to simulate a normalised (0-1) file.</summary>
    internal static Func<int, int, double> Scaled(Func<int, int, double> b, double factor) =>
        (x, y) => b(x, y) * factor;

    internal static Func<int, int, double> Constant(double v) => (x, y) => v;
}
