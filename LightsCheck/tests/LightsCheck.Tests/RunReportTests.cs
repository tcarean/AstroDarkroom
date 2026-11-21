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

using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The one formatter of the figures the user reads, <see cref="RunReport.F"/>, and its signed
/// form: a figure that rounds to zero carries no minus (R-092, ADR 0066).
/// </summary>
public class RunReportTests
{
    /// <summary>
    /// A negative figure whose written digits are all 0 loses its minus; one that keeps a digit
    /// keeps its sign, and a positive figure is as before.
    /// </summary>
    [Theory]
    [InlineData(-0.0004, 3, "0.000")]
    [InlineData(-0.04, 1, "0.0")]
    [InlineData(-0.4, 0, "0")]
    [InlineData(-0.0, 2, "0.00")]
    [InlineData(0.04, 1, "0.0")]
    [InlineData(-0.06, 1, "-0.1")]
    [InlineData(-12.34, 1, "-12.3")]
    [InlineData(12.34, 1, "12.3")]
    public void R092_AFigureThatRoundsToZeroIsWrittenWithoutAMinus(double value, int decimals, string expected)
        => Assert.Equal(expected, RunReport.F(value, decimals));

    /// <summary>
    /// Where the sign is always written, a figure that rounds to zero reads `+0`, whichever
    /// its sign was; the sign of any other figure is its own.
    /// </summary>
    [Theory]
    [InlineData(-0.004, 0, "+0")]
    [InlineData(-0.004, 2, "+0.00")]
    [InlineData(0.0, 1, "+0.0")]
    [InlineData(-0.006, 2, "-0.01")]
    [InlineData(1.25, 2, "+1.25")]
    [InlineData(-1.25, 2, "-1.25")]
    public void R092_ASignedFigureThatRoundsToZeroReadsPlusZero(double value, int decimals, string expected)
        => Assert.Equal(expected, RunReport.Signed(value, decimals));

    /// <summary>
    /// A figure as it is shown is its written digits read back, so a word decided on it cannot
    /// stand against the digits beside it (R-024, ADR 0067).
    /// </summary>
    [Theory]
    [InlineData(9.96, 1, 10.0)]
    [InlineData(9.94, 1, 9.9)]
    [InlineData(10.04, 1, 10.0)]
    [InlineData(1.597, 2, 1.6)]
    [InlineData(-0.04, 1, 0.0)]
    public void R024_AFigureAsShownIsItsWrittenDigitsReadBack(double value, int decimals, double expected)
        => Assert.Equal(expected, RunReport.Shown(value, decimals));
}
