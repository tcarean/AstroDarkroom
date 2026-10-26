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

namespace ImageVisualCompare.Core;

/// <summary>
/// The level each of the 65 536 sixteen-bit values is shown at: the linear rule of
/// <see cref="FitsScale.Level"/>, or the equalisation a <see cref="SampleHistogram"/> gives
/// (R-111). One lookup per sample is what the decode does, whatever the mode, so that nothing
/// is decided per sample.
/// </summary>
public sealed class LevelLookup
{
    /// <summary>How many sixteen-bit values there are, and how long <see cref="Table"/> is.</summary>
    public const int Size = 65536;

    /// <summary>
    /// The linear display: the high byte of the sixteen-bit value, as WIC keeps of a 16-bit PNG
    /// (ADR 0029).
    /// </summary>
    public static LevelLookup Linear { get; } = BuildLinear();

    /// <summary>Builds a lookup over a table of <see cref="Size"/> levels.</summary>
    /// <param name="table">The level of each sixteen-bit value, indexed by the value.</param>
    private LevelLookup(byte[] table)
    {
        Table = table;
    }

    /// <summary>The level of each sixteen-bit value, indexed by the value; <see cref="Size"/> long.</summary>
    public byte[] Table { get; }

    /// <summary>Gives the level of a sixteen-bit value.</summary>
    /// <param name="sixteenBit">The value, 0 to 65 535.</param>
    /// <returns>The level, 0 to 255.</returns>
    public byte Level(ushort sixteenBit) => Table[sixteenBit];

    /// <summary>
    /// Siril's <c>round_to_BYTE</c>: the nearest level, halves up, capped at 255. Not
    /// <c>Math.Round</c>, which rounds halves to even.
    /// </summary>
    /// <param name="value">A level before rounding, 0 or above.</param>
    /// <returns>The level, 0 to 255.</returns>
    public static byte RoundToByte(double value) => (byte)Math.Min(255, (int)(value + 0.5));

    /// <summary>
    /// Builds the equalisation of a histogram, as Siril's "Histogram" display does: level 0
    /// for value 0, and for every other value the share of the image's pixels at or below it,
    /// times 255, rounded with <see cref="RoundToByte"/>. The pixels in bin 0, never counted,
    /// still divide, so an image with many of them never reaches 255 (R-111).
    /// </summary>
    /// <param name="histogram">The counts of one layer, with how many pixels the layer has.</param>
    /// <returns>The lookup; every level 0 when the layer has no pixels.</returns>
    public static LevelLookup Equalising(SampleHistogram histogram)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        var table = new byte[Size];
        if (histogram.Pixels == 0)
        {
            return new LevelLookup(table);
        }

        double pixels = histogram.Pixels;
        long cumulative = 0;
        for (int value = 1; value < Size; value++)
        {
            cumulative += histogram.Counts[value];
            table[value] = RoundToByte(cumulative / pixels * 255.0);
        }

        return new LevelLookup(table);
    }

    /// <summary>Builds <see cref="Linear"/>.</summary>
    /// <returns>The lookup whose level is the high byte of the value.</returns>
    private static LevelLookup BuildLinear()
    {
        var table = new byte[Size];
        for (int value = 0; value < Size; value++)
        {
            table[value] = (byte)(value >> 8);
        }

        return new LevelLookup(table);
    }
}
