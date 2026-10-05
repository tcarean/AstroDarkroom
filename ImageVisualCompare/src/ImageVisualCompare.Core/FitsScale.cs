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
/// How a FITS value becomes one of the 256 levels on screen: linearly, with no stretch, and by
/// the rule WIC applies to a 16-bit PNG, so that a FITS and its own export show the same
/// levels on both sides of the divider (ADR 0029).
/// </summary>
public static class FitsScale
{
    /// <summary>
    /// A float file whose largest sample is above this is taken to count in 0 to 65535, not in
    /// 0 to 1. The largest sample of five Siril files measured on 2026-10-05 was 1.0005; a file
    /// in 0 to 65535 that stays under 2 would be black on any scale.
    /// </summary>
    public const double RescaleThreshold = 2.0;

    /// <summary>What <see cref="ImageInfo.ValueRange"/> says of a float file read in 0 to 65535.</summary>
    public const string SixteenBitRange = "0..65535";

    private const double SixteenBitMax = 65535;

    /// <summary>The value that is white, for a sample type and, for floats, the largest sample seen.</summary>
    public static double FullScale(int bitPix, double largestSample = 0) => bitPix switch
    {
        8 => 255,
        16 => SixteenBitMax,
        32 => 4294967295,
        _ => IsRescaled(bitPix, largestSample) ? SixteenBitMax : 1,
    };

    public static bool IsRescaled(int bitPix, double largestSample) =>
        bitPix < 0 && largestSample > RescaleThreshold;

    /// <summary>
    /// The level of a value: through 16 bits, then the high byte, which is what WIC keeps of a
    /// 16-bit sample (measured on all 65536 values). NaN, which Siril leaves behind after some
    /// float operations, is black.
    /// </summary>
    public static byte ToByte(double value, double fullScale)
    {
        double fraction = value / fullScale;
        if (!(fraction > 0))
        {
            return 0;
        }

        if (fraction >= 1)
        {
            return 255;
        }

        return (byte)((int)((fraction * SixteenBitMax) + 0.5) >> 8);
    }
}
