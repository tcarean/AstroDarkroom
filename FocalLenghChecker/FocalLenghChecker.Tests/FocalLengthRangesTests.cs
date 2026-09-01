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

namespace FocalLenghChecker.Tests;

public class FocalLengthRangesTests
{
    [Fact]
    public void The_shipped_ranges_are_valid()
    {
        FocalLengthRanges.Validate(FocalLengthRanges.Default);
    }

    [Fact]
    public void The_shipped_ranges_are_the_ones_that_were_asked_for()
    {
        Assert.Equal(
            new[]
            {
                new FocalLengthRange(16, 23),
                new FocalLengthRange(24, 27),
                new FocalLengthRange(28, 70),
                new FocalLengthRange(71, 200),
            },
            FocalLengthRanges.Default);
    }

    [Fact]
    public void The_shipped_crop_factor_is_one_and_a_half()
    {
        Assert.Equal(1.5, FocalLengthRanges.CropFactor);
    }

    [Fact]
    public void An_empty_set_of_ranges_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => FocalLengthRanges.Validate(Array.Empty<FocalLengthRange>()));
    }

    [Fact]
    public void Null_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => FocalLengthRanges.Validate(null!));
    }

    [Fact]
    public void A_range_that_ends_before_it_starts_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            FocalLengthRanges.Validate(new[] { new FocalLengthRange(70, 28) }));
    }

    [Fact]
    public void A_range_starting_at_zero_or_below_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            FocalLengthRanges.Validate(new[] { new FocalLengthRange(0, 23) }));
    }

    [Fact]
    public void Overlapping_ranges_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            FocalLengthRanges.Validate(new[] { new FocalLengthRange(16, 30), new FocalLengthRange(28, 70) }));
    }

    [Fact]
    public void Ranges_that_touch_only_through_the_exclusive_upper_bound_are_rejected()
    {
        // 16-23 already covers everything below 24, so a range starting at 23 overlaps it.
        Assert.Throws<ArgumentException>(() =>
            FocalLengthRanges.Validate(new[] { new FocalLengthRange(16, 23), new FocalLengthRange(23, 27) }));
    }

    [Fact]
    public void Descending_ranges_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            FocalLengthRanges.Validate(new[] { new FocalLengthRange(28, 70), new FocalLengthRange(16, 23) }));
    }

    [Fact]
    public void Ranges_with_a_deliberate_gap_between_them_are_allowed()
    {
        FocalLengthRanges.Validate(new[] { new FocalLengthRange(16, 23), new FocalLengthRange(100, 200) });
    }
}
