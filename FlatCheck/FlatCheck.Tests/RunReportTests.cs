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

namespace FlatCheck.Tests;

/// <summary>
/// The two pieces of wording that go with the map. Neither has a console caller today -
/// only the window does - but they live in Report/ like every other sentence, so a
/// future --scala flag would reuse them instead of rewriting them.
/// </summary>
public class RunReportTests
{
    [Fact]
    public void TheAutomaticScaleIsLabelledAuto()
    {
        Assert.Equal("auto", RunReport.MapScaleLabel(null));
    }

    [Fact]
    public void AFixedScaleIsLabelledWithItsCap()
    {
        Assert.Equal("+/-2 %", RunReport.MapScaleLabel(2));
        Assert.Equal("+/-10 %", RunReport.MapScaleLabel(10));
        Assert.Equal("+/-20 %", RunReport.MapScaleLabel(20));
    }

    /// <summary>
    /// The whole point of the legend: the ends of the displayed scale, stated in the
    /// same percent the four tests are stated in.
    /// </summary>
    [Fact]
    public void TheLegendOnAFixedScaleStatesTheCapOnBothSides()
    {
        Assert.Equal(
            "albastru = -10.0 %   alb = 0 (exact pe model)   rosu = +10.0 %",
            RunReport.MapLegend(PngWriter.FixedRange(10)));
    }

    /// <summary>
    /// On the automatic scale the two ends differ, which is exactly what could not be
    /// seen on screen before - so both are printed, each with its own sign.
    /// </summary>
    [Fact]
    public void TheLegendOnAnAsymmetricRangePrintsBothEndsSigned()
    {
        string s = RunReport.MapLegend(new PngWriter.MapRange(0.968, 1.024));

        Assert.Equal(
            "albastru = -3.2 %   alb = 0 (exact pe model)   rosu = +2.4 %",
            s);
    }

    /// <summary>
    /// A range that does not straddle 1.0 cannot happen with today's callers, but the
    /// sign must still come out right rather than printing a bare "3.2".
    /// </summary>
    [Fact]
    public void BothEndsKeepTheirSignEvenWhenTheyPointTheSameWay()
    {
        string s = RunReport.MapLegend(new PngWriter.MapRange(1.01, 1.03));

        Assert.Contains("albastru = +1.0 %", s);
        Assert.Contains("rosu = +3.0 %", s);
    }
}
