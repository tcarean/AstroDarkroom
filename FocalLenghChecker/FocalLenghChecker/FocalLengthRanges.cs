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

namespace FocalLenghChecker;

/// <summary>
/// The tunable configuration for the whole tool. To change which focal length
/// buckets get reported, edit <see cref="Default"/> below -- nothing else needs
/// to change. Values are full-frame (FF) equivalent millimetres, inclusive.
/// </summary>
public static class FocalLengthRanges
{
    /// <summary>
    /// The DX to full-frame crop factor. A full-frame equivalent focal length
    /// is CropFactor times the DX focal length.
    /// </summary>
    public const double CropFactor = 1.5;

    /// <summary>
    /// The reported ranges, in full-frame equivalent millimetres.
    /// Must be listed in ascending order and must not overlap.
    /// </summary>
    public static readonly IReadOnlyList<FocalLengthRange> Default = new[]
    {
        new FocalLengthRange(16, 23),
        new FocalLengthRange(24, 27),
        new FocalLengthRange(28, 70),
        new FocalLengthRange(71, 200),
    };

    /// <summary>
    /// Checks that a set of ranges is usable: non-empty, each range well formed,
    /// listed in ascending order and non-overlapping. Throws if it is not, so a
    /// bad edit to <see cref="Default"/> fails loudly instead of silently
    /// double-counting or losing focal lengths.
    /// </summary>
    public static void Validate(IReadOnlyList<FocalLengthRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        if (ranges.Count == 0)
            throw new ArgumentException("At least one focal length range must be configured.", nameof(ranges));

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];

            if (range.MinFf <= 0)
                throw new ArgumentException(
                    $"Range {range.FfLabel} starts at {range.MinFf} mm; focal lengths must be positive.", nameof(ranges));

            if (range.MaxFf < range.MinFf)
                throw new ArgumentException(
                    $"Range {range.FfLabel} ends before it starts.", nameof(ranges));

            if (i > 0 && range.MinFf < ranges[i - 1].UpperBoundExclusive)
                throw new ArgumentException(
                    $"Range {range.FfLabel} overlaps or is out of order with {ranges[i - 1].FfLabel}.", nameof(ranges));
        }
    }
}
