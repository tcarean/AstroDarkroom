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
/// The histogram of one layer over its sixteen-bit values, as Siril computes it for its
/// "Histogram" display: one bin per value, and a sample at 0 left out of the bins but counted
/// among the pixels (R-111, ADR 0040).
/// </summary>
public sealed class SampleHistogram
{
    /// <summary>How many samples fell in each bin, indexed by the sixteen-bit value; bin 0 stays 0.</summary>
    public long[] Counts { get; } = new long[LevelLookup.Size];

    /// <summary>How many samples were counted, those at 0 included.</summary>
    public long Pixels { get; private set; }

    /// <summary>Counts one sample.</summary>
    /// <param name="sixteenBit">The sample's sixteen-bit value, 0 to 65 535.</param>
    public void Count(ushort sixteenBit)
    {
        Pixels++;
        if (sixteenBit != 0)
        {
            Counts[sixteenBit]++;
        }
    }

    /// <summary>Counts every sample of a row.</summary>
    /// <param name="row">The samples' sixteen-bit values.</param>
    public void Count(ReadOnlySpan<ushort> row)
    {
        long[] counts = Counts;
        foreach (ushort value in row)
        {
            // Bin 0 is counted and then zeroed, rather than tested per sample.
            counts[value]++;
        }

        Pixels += row.Length;
        counts[0] = 0;
    }

    /// <summary>Forgets every count, so the same histogram serves a second pass over the file.</summary>
    public void Clear()
    {
        Array.Clear(Counts);
        Pixels = 0;
    }
}
