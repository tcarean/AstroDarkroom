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

// CfaPattern.cs - which colour each of the four Bayer positions has.
//
// WHY IT IS NOT A CONSTANT. RGGB is the most common order, but not the only one: there are
// cameras with GBRG, GRBG and BGGR. The order is read from the file - from libraw on a raw, from
// the BAYERPAT card on a FITS made through Siril's Conversion tab - and when the file does not
// say it stays null and the positions stay anonymous. The tool does not guess: an "R" label put
// on the wrong channel would be worse than none, because it would look measured.
//
// MIND INDEX 3. A position's colour is taken from cdesc, not straight from the FC code: libraw
// sometimes puts the second green on index 3 instead of 1 (cdesc = "RGBG"). Measured on a D3300
// NEF: FC gives 0/1/3/2, and through cdesc it comes out RGGB. A decoder that assumes 1 = G would
// read the second green as something else.

using System;

namespace FlatCheck
{
    /// <summary>
    /// The colour of the four Bayer positions of the LOADED raster - [0,0], [0,1], [1,0],
    /// [1,1] - in the order ExposureAnalyzer numbers them too.
    /// </summary>
    internal sealed class CfaPattern
    {
        /// <summary>Exactly 4 letters from {R,G,B}, exactly two of them G.</summary>
        internal readonly char[] Colours;

        /// <summary>
        /// Where the order was read from: "libraw" or "BAYERPAT". Shown in the report.
        /// </summary>
        internal readonly string Source;

        /// <summary>A pattern from letters already checked by <see cref="Parse"/>.</summary>
        /// <param name="colours">The four letters, in raster order.</param>
        /// <param name="source">Where they were read from.</param>
        CfaPattern(char[] colours, string source)
        {
            Colours = colours;
            Source = source;
        }

        /// <summary>The four letters as a string, in raster order: "RGGB".</summary>
        internal string Letters => new string(Colours);

        /// <summary>
        /// From the four letters, if they are a valid Bayer mosaic: four characters from {R,G,B}
        /// with exactly two G. Anything else - another length, foreign letters, three greens, a
        /// CFA with white - returns null, that is "I do not know", which is the right answer.
        /// </summary>
        /// <param name="letters">The letters, any case, surrounding blanks allowed; null
        /// allowed.</param>
        /// <param name="source">Where they were read from, kept as <see cref="Source"/>.</param>
        /// <returns>The pattern, or null when the letters are not a Bayer mosaic.</returns>
        internal static CfaPattern Parse(string letters, string source)
        {
            if (string.IsNullOrEmpty(letters)) return null;

            string s = letters.Trim().ToUpperInvariant();
            if (s.Length != 4) return null;

            int greens = 0;
            foreach (char c in s)
            {
                if (c == 'G') greens++;
                else if (c != 'R' && c != 'B') return null;
            }
            if (greens != 2) return null;

            return new CfaPattern(s.ToCharArray(), source);
        }

        /// <summary>
        /// From libraw: the <paramref name="filters"/> mask plus <paramref name="cdesc"/>,
        /// evaluated at the origin of the VISIBLE area. The offsets are TopMargin/LeftMargin,
        /// because RawReader crops the masked margins - and an odd offset shifts the mosaic's
        /// phase by one position, so it would give a wrong order with the air of a measurement.
        ///
        /// FC(row, col) is dcraw's macro, and it returns an INDEX into cdesc, not a colour. The
        /// mask's special values are refused: 0 = no CFA (mono, Foveon), 1 = Leaf Catchlight,
        /// 9 = X-Trans (a 6x6 mosaic, not 2x2).
        /// </summary>
        /// <param name="filters">libraw's <c>filters</c> mask.</param>
        /// <param name="cdesc">libraw's colour description, at least four letters.</param>
        /// <param name="rowOffset">The first visible row on the sensor (TopMargin).</param>
        /// <param name="colOffset">The first visible column on the sensor (LeftMargin).</param>
        /// <returns>The pattern, or null for a special mask or a short cdesc.</returns>
        internal static CfaPattern FromLibRaw(uint filters, string cdesc, int rowOffset, int colOffset)
        {
            if (filters == 0 || filters == 1 || filters == 9) return null;
            if (string.IsNullOrEmpty(cdesc) || cdesc.Length < 4) return null;

            var c = new char[4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    int row = rowOffset + y, col = colOffset + x;
                    int shift = ((((row << 1) & 14) + (col & 1)) << 1);
                    int idx = (int)((filters >> shift) & 3);
                    c[y * 2 + x] = char.ToUpperInvariant(cdesc[idx]);
                }

            return Parse(new string(c), "libraw");
        }

        /// <summary>
        /// The same mosaic seen from (dy, dx). For XBAYROFF / YBAYROFF of a FITS, which Siril
        /// writes when the frame was cropped relative to the sensor.
        /// </summary>
        /// <param name="dy">The row offset; only its parity matters.</param>
        /// <param name="dx">The column offset; only its parity matters.</param>
        /// <returns>The shifted pattern, or this one when both offsets are even.</returns>
        internal CfaPattern Shift(int dy, int dx)
        {
            if ((dy & 1) == 0 && (dx & 1) == 0) return this;

            var c = new char[4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    c[y * 2 + x] = Colours[(((y + dy) & 1) * 2) + ((x + dx) & 1)];

            return new CfaPattern(c, Source);
        }

        /// <summary>
        /// The same mosaic as it lies in the RASTER, whose row 0 is the file's last row (R-130).
        /// BAYERPAT describes the top left corner of the sensor, seen top down. Siril writes the
        /// rows bottom up (ROWORDER = 'BOTTOM-UP'), so read the other way round the raster is the
        /// sensor seen top down and the pattern applies as written, at any height. A file not
        /// written bottom up (TOP-DOWN, no card, anything else) is read upside down against its
        /// pattern: on an EVEN height, row 0 of the raster is an odd row of the sensor and the
        /// mosaic's phase moves by one row, RGGB becomes GBRG; on an odd height the last row has
        /// the first row's parity and nothing changes.
        ///
        /// Measured on pp_flat_stacked.fit (6016x4016, Tokina, blue dusk sky), when the rows were
        /// still read in file order: uncorrected, --histeq named "G2" the position [1,0], which
        /// sat at a third of the green - that is, exactly the red --expo sees on the source NEF,
        /// through libraw. Read with the file's first row last, R sits at [0,0], as on the NEF.
        /// </summary>
        /// <param name="rowOrder">The ROWORDER card's value, or null when there is none.</param>
        /// <param name="height">The frame's height in rows.</param>
        /// <returns>The pattern in the raster's row order; this one when nothing changes.</returns>
        internal CfaPattern ForRowOrder(string rowOrder, int height)
        {
            bool bottomUp = !string.IsNullOrEmpty(rowOrder)
                && string.Equals(rowOrder.Trim(), "BOTTOM-UP", StringComparison.OrdinalIgnoreCase);
            if (bottomUp) return this;
            return (height & 1) == 0 ? Shift(1, 0) : this;
        }

        /// <summary>
        /// The position's name: "R", "G1", "G2", "B". The greens are numbered in raster order,
        /// because there are two and they must be told apart on screen - on a correct mosaic
        /// they have almost the same level, and that "almost" is exactly what is read.
        /// </summary>
        /// <param name="i">The position, 0..3: [0,0], [0,1], [1,0], [1,1].</param>
        /// <returns>The position's name.</returns>
        internal string Name(int i)
        {
            char c = Colours[i];
            if (c != 'G') return c.ToString();
            return i < IndexOfSecondGreen ? "G1" : "G2";
        }

        int IndexOfSecondGreen
        {
            get
            {
                int seen = 0;
                for (int i = 0; i < 4; i++)
                    if (Colours[i] == 'G' && ++seen == 2) return i;
                return 4;   // cannot happen: Parse guarantees two
            }
        }
    }
}
