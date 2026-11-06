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

namespace FlatCheck.Tests;

/// <summary>
/// The shared signed format (R-129): the sign is always written, and what rounds to zero at the
/// decimals shown is "+0.00", whatever its sign was - the rule LocalSpotSize had alone before.
/// </summary>
public class NumbersTests
{
    [Theory]
    [InlineData(0.34, 2, "+0.34")]
    [InlineData(-0.34, 2, "-0.34")]
    [InlineData(0.0, 2, "+0.00")]                  // -0.0 is the same InlineData to xUnit: ATinyNegativeNeverShowsAMinus has it
    [InlineData(0.004, 2, "+0.00")]
    [InlineData(-0.004, 2, "+0.00")]
    [InlineData(-0.001, 2, "+0.00")]
    [InlineData(-0.006, 2, "-0.01")]
    [InlineData(0.005, 2, "+0.01")]                // the midpoint, rounded as the old section format did
    [InlineData(-0.005, 2, "-0.01")]
    [InlineData(-0.04, 1, "+0.0")]
    [InlineData(-0.06, 1, "-0.1")]
    [InlineData(-3.2, 1, "-3.2")]
    [InlineData(-0.4, 0, "+0")]
    [InlineData(-0.6, 0, "-1")]
    [InlineData(52.2, 0, "+52")]
    [InlineData(123.456, 2, "+123.46")]
    public void TheSignIsWrittenAndAZeroIsPlusZero(double value, int decimals, string expected)
    {
        Assert.Equal(expected, Numbers.Signed(value, decimals));
    }

    /// <summary>The sign of a value that rounds to zero never leaks, whatever the magnitude.</summary>
    [Theory]
    [InlineData(-1e-15)]
    [InlineData(-0.00499)]
    [InlineData(-0.0)]
    public void ATinyNegativeNeverShowsAMinus(double value)
    {
        Assert.Equal("+0.00", Numbers.Signed(value, 2));
    }

    /// <summary>The same rounding decides the word beside the digits.</summary>
    [Theory]
    [InlineData(-0.004, 2, true)]
    [InlineData(-0.006, 2, false)]
    [InlineData(0.04, 1, true)]
    [InlineData(0.4, 0, true)]
    [InlineData(0.6, 0, false)]
    [InlineData(double.NaN, 2, false)]
    public void RoundsToZeroFollowsTheDecimalsShown(double value, int decimals, bool expected)
    {
        Assert.Equal(expected, Numbers.RoundsToZero(value, decimals));
    }

    /// <summary>
    /// The provider given is the one used (a hand-made comma, since the test assembly's current
    /// culture is invariant and would write a dot either way); NaN and the infinities come back
    /// as .NET writes them, with no sign of the helper's own.
    /// </summary>
    [Fact]
    public void TheProviderAndTheNonFiniteValuesPassThrough()
    {
        var comma = new NumberFormatInfo { NumberDecimalSeparator = "," };

        Assert.Equal("-3,2", Numbers.Signed(-3.2, 1, comma));
        Assert.Equal("-3.2", Numbers.Signed(-3.2, 1, CultureInfo.InvariantCulture));
        Assert.Equal("NaN", Numbers.Signed(double.NaN, 2));
        Assert.Equal("Infinity", Numbers.Signed(double.PositiveInfinity, 2));
        Assert.Equal("-Infinity", Numbers.Signed(double.NegativeInfinity, 2));
        Assert.False(Numbers.RoundsToZero(double.PositiveInfinity, 2));
        Assert.False(Numbers.RoundsToZero(double.NegativeInfinity, 2));
    }
}
